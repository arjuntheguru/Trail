import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

import { unwrap } from '@/api/unwrap'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { FormField } from '@/components/FormField'
import { Button } from '@/components/ui/button'

import { applyServerError } from './formErrors'
import { departmentsQuery, type Department } from './queries'
import {
  ConfirmDialog,
  EmptyState,
  FormDialog,
  ListSkeleton,
  LoadError,
  RequirePermission,
  SectionHeader,
} from './shared'
import { useSettingsMutation } from './useSettingsMutation'

// Mirrors Department.NameMaxLength; the server stays the authority.
const departmentSchema = z.object({
  name: z.string().trim().min(1, 'Enter a name.').max(100, 'The name is too long.'),
})
type DepartmentValues = z.infer<typeof departmentSchema>

export function DepartmentsPage() {
  return (
    <RequirePermission permission={Permissions.Sites.View}>
      <DepartmentsSection />
    </RequirePermission>
  )
}

function DepartmentsSection() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canManage = usePermission(Permissions.Sites.Manage)
  const departments = useQuery(departmentsQuery(client, organizationId))
  const [editing, setEditing] = useState<Department | 'new' | null>(null)
  const [deleting, setDeleting] = useState<Department | null>(null)

  return (
    <section className="flex flex-col gap-4">
      <SectionHeader
        title="Departments"
        description="The teams or functions people work in, such as Kitchen or Front of house."
        action={
          canManage && (
            <Button onClick={() => setEditing('new')}>
              <Plus aria-hidden />
              Add department
            </Button>
          )
        }
      />
      {departments.isPending ? (
        <ListSkeleton />
      ) : departments.isError ? (
        <LoadError error={departments.error} retry={() => void departments.refetch()} />
      ) : departments.data.length === 0 ? (
        <EmptyState>No departments yet.</EmptyState>
      ) : (
        <ul className="flex flex-col gap-2">
          {departments.data.map((department) => (
            <li
              key={department.id}
              className="flex items-center gap-3 rounded-xl border bg-card p-3"
            >
              <span className="min-w-0 flex-1 truncate font-medium">{department.name}</span>
              {canManage && (
                <>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Edit department ${department.name}`}
                    onClick={() => setEditing(department)}
                  >
                    <Pencil aria-hidden />
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Delete department ${department.name}`}
                    onClick={() => setDeleting(department)}
                  >
                    <Trash2 aria-hidden />
                  </Button>
                </>
              )}
            </li>
          ))}
        </ul>
      )}
      {editing && (
        <DepartmentDialog
          department={editing === 'new' ? undefined : editing}
          onClose={() => setEditing(null)}
        />
      )}
      {deleting && (
        <DeleteDepartmentDialog department={deleting} onClose={() => setDeleting(null)} />
      )}
    </section>
  )
}

function DepartmentDialog({
  department,
  onClose,
}: {
  department?: Department
  onClose: () => void
}) {
  const { client } = useAuthService()
  const [general, setGeneral] = useState<string[]>([])
  const form = useForm<DepartmentValues>({
    resolver: zodResolver(departmentSchema),
    defaultValues: { name: department?.name ?? '' },
  })
  const mutation = useSettingsMutation({
    run: (body: DepartmentValues) =>
      department
        ? unwrap(
            client.PUT('/api/v1/departments/{id}', {
              params: { path: { id: department.id } },
              body,
            }),
          )
        : unwrap(client.POST('/api/v1/departments', { body })),
    areas: [['departments']],
    onSuccess: () => {
      toast.success(department ? 'Department saved.' : 'Department added.')
      onClose()
    },
  })
  const submit = form.handleSubmit((values) => {
    setGeneral([])
    mutation.mutate(values, {
      onError: (error) =>
        setGeneral(
          applyServerError(error, ['name'], form.setError, { 'department.name_taken': 'name' }),
        ),
    })
  })

  return (
    <FormDialog
      title={department ? 'Edit department' : 'Add department'}
      submitLabel={department ? 'Save' : 'Add department'}
      pending={mutation.isPending}
      errors={general}
      onSubmit={(e) => void submit(e)}
      onClose={onClose}
    >
      <FormField
        label="Name"
        autoComplete="off"
        error={form.formState.errors.name}
        {...form.register('name')}
      />
    </FormDialog>
  )
}

function DeleteDepartmentDialog({
  department,
  onClose,
}: {
  department: Department
  onClose: () => void
}) {
  const { client } = useAuthService()
  return (
    <ConfirmDialog
      title="Delete department?"
      description={`"${department.name}" will be removed and taken off every member it was assigned to.`}
      confirmLabel="Delete department"
      successMessage="Department deleted."
      areas={[['departments'], ['members']]}
      run={() =>
        unwrap(
          client.DELETE('/api/v1/departments/{id}', { params: { path: { id: department.id } } }),
        )
      }
      onClose={onClose}
    />
  )
}
