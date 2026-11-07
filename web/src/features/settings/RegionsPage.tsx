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
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'

import { applyServerError } from './formErrors'
import { regionsQuery, type Region } from './queries'
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

// Mirrors Region.NameMaxLength / DescriptionMaxLength; the server stays the authority.
const regionSchema = z.object({
  name: z.string().trim().min(1, 'Enter a name.').max(100, 'The name is too long.'),
  description: z.string().trim().max(500, 'The description is too long.'),
})
type RegionValues = z.infer<typeof regionSchema>

export function RegionsPage() {
  return (
    <RequirePermission permission={Permissions.Sites.View}>
      <RegionsSection />
    </RequirePermission>
  )
}

function RegionsSection() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canManage = usePermission(Permissions.Sites.Manage)
  const regions = useQuery(regionsQuery(client, organizationId))
  const [editing, setEditing] = useState<Region | 'new' | null>(null)
  const [deleting, setDeleting] = useState<Region | null>(null)

  return (
    <section className="flex flex-col gap-4">
      <SectionHeader
        title="Regions"
        description="Group sites by area. Members can be given access to a whole region."
        action={
          canManage && (
            <Button onClick={() => setEditing('new')}>
              <Plus aria-hidden />
              Add region
            </Button>
          )
        }
      />
      {regions.isPending ? (
        <ListSkeleton />
      ) : regions.isError ? (
        <LoadError error={regions.error} retry={() => void regions.refetch()} />
      ) : regions.data.length === 0 ? (
        <EmptyState>No regions yet.</EmptyState>
      ) : (
        <ul className="flex flex-col gap-2">
          {regions.data.map((region) => (
            <li key={region.id} className="flex items-center gap-3 rounded-xl border bg-card p-3">
              <div className="min-w-0 flex-1">
                <p className="truncate font-medium">{region.name}</p>
                {region.description && (
                  <p className="truncate text-sm text-muted-foreground">{region.description}</p>
                )}
              </div>
              {canManage && (
                <>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Edit region ${region.name}`}
                    onClick={() => setEditing(region)}
                  >
                    <Pencil aria-hidden />
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Delete region ${region.name}`}
                    onClick={() => setDeleting(region)}
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
        <RegionDialog
          region={editing === 'new' ? undefined : editing}
          onClose={() => setEditing(null)}
        />
      )}
      {deleting && <DeleteRegionDialog region={deleting} onClose={() => setDeleting(null)} />}
    </section>
  )
}

function RegionDialog({ region, onClose }: { region?: Region; onClose: () => void }) {
  const { client } = useAuthService()
  const [general, setGeneral] = useState<string[]>([])
  const form = useForm<RegionValues>({
    resolver: zodResolver(regionSchema),
    defaultValues: { name: region?.name ?? '', description: region?.description ?? '' },
  })
  const mutation = useSettingsMutation({
    run: (values: RegionValues) => {
      const body = { name: values.name, description: values.description || null }
      return region
        ? unwrap(client.PUT('/api/v1/regions/{id}', { params: { path: { id: region.id } }, body }))
        : unwrap(client.POST('/api/v1/regions', { body }))
    },
    // Sites show their region's name, and access editors list regions.
    areas: [['regions'], ['sites']],
    onSuccess: () => {
      toast.success(region ? 'Region saved.' : 'Region added.')
      onClose()
    },
  })
  const submit = form.handleSubmit((values) => {
    setGeneral([])
    mutation.mutate(values, {
      onError: (error) =>
        setGeneral(
          applyServerError(error, ['name', 'description'], form.setError, {
            'region.name_taken': 'name',
          }),
        ),
    })
  })
  const descriptionError = form.formState.errors.description

  return (
    <FormDialog
      title={region ? 'Edit region' : 'Add region'}
      submitLabel={region ? 'Save' : 'Add region'}
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
      <div className="flex flex-col gap-2">
        <Label htmlFor="region-description">Description</Label>
        <Textarea
          id="region-description"
          rows={3}
          aria-invalid={descriptionError ? true : undefined}
          {...form.register('description')}
        />
        {descriptionError && (
          <p role="alert" className="text-sm text-destructive">
            {descriptionError.message}
          </p>
        )}
      </div>
    </FormDialog>
  )
}

function DeleteRegionDialog({ region, onClose }: { region: Region; onClose: () => void }) {
  const { client } = useAuthService()
  return (
    <ConfirmDialog
      title="Delete region?"
      description={`"${region.name}" will be removed, along with any member access granted through it. A region that still has sites cannot be deleted.`}
      confirmLabel="Delete region"
      successMessage="Region deleted."
      areas={[['regions'], ['members']]}
      run={() =>
        unwrap(client.DELETE('/api/v1/regions/{id}', { params: { path: { id: region.id } } }))
      }
      onClose={onClose}
    />
  )
}
