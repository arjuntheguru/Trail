import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

import { unwrap } from '@/api/unwrap'
import { useAuthService, usePermission, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { FormField } from '@/components/FormField'
import { Button } from '@/components/ui/button'

import { applyServerError } from './formErrors'
import { tagsQuery, type Tag } from './queries'
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

// Mirrors the API's limits (Tag.NameMaxLength, hex colour pattern); the server stays the authority.
const COLOR = /^#[0-9A-Fa-f]{6}$/
const tagSchema = z.object({
  name: z.string().trim().min(1, 'Enter a name.').max(64, 'The name is too long.'),
  color: z
    .string()
    .trim()
    .refine((v) => v === '' || COLOR.test(v), 'Use a hex colour such as #1F8A70.'),
})
type TagValues = z.infer<typeof tagSchema>

export function TagsPage() {
  return (
    <RequirePermission permission={Permissions.Templates.View}>
      <TagsSection />
    </RequirePermission>
  )
}

function TagsSection() {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const canManage = usePermission(Permissions.Tags.Manage)
  const tags = useQuery(tagsQuery(client, organizationId))
  const [editing, setEditing] = useState<Tag | 'new' | null>(null)
  const [deleting, setDeleting] = useState<Tag | null>(null)

  return (
    <section className="flex flex-col gap-4">
      <SectionHeader
        title="Tags"
        description="Labels used to group templates and tasks."
        action={
          canManage && (
            <Button onClick={() => setEditing('new')}>
              <Plus aria-hidden />
              Add tag
            </Button>
          )
        }
      />
      {tags.isPending ? (
        <ListSkeleton />
      ) : tags.isError ? (
        <LoadError error={tags.error} retry={() => void tags.refetch()} />
      ) : tags.data.length === 0 ? (
        <EmptyState>No tags yet.</EmptyState>
      ) : (
        <ul className="flex flex-col gap-2">
          {tags.data.map((tag) => (
            <li key={tag.id} className="flex items-center gap-3 rounded-xl border bg-card p-3">
              <span
                aria-hidden
                className="size-5 shrink-0 rounded-full border"
                style={{ backgroundColor: tag.color ?? 'transparent' }}
              />
              <span className="min-w-0 flex-1 truncate font-medium">{tag.name}</span>
              {canManage && (
                <>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Edit tag ${tag.name}`}
                    onClick={() => setEditing(tag)}
                  >
                    <Pencil aria-hidden />
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Delete tag ${tag.name}`}
                    onClick={() => setDeleting(tag)}
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
        <TagDialog tag={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />
      )}
      {deleting && <DeleteTagDialog tag={deleting} onClose={() => setDeleting(null)} />}
    </section>
  )
}

function TagDialog({ tag, onClose }: { tag?: Tag; onClose: () => void }) {
  const { client } = useAuthService()
  const [general, setGeneral] = useState<string[]>([])
  const form = useForm<TagValues>({
    resolver: zodResolver(tagSchema),
    defaultValues: { name: tag?.name ?? '', color: tag?.color ?? '' },
  })
  const mutation = useSettingsMutation({
    run: (values: TagValues) => {
      const body = { name: values.name, color: values.color || null }
      return tag
        ? unwrap(client.PUT('/api/v1/tags/{id}', { params: { path: { id: tag.id } }, body }))
        : unwrap(client.POST('/api/v1/tags', { body }))
    },
    areas: [['tags']],
    onSuccess: () => {
      toast.success(tag ? 'Tag saved.' : 'Tag added.')
      onClose()
    },
  })
  const submit = form.handleSubmit((values) => {
    setGeneral([])
    mutation.mutate(values, {
      onError: (error) =>
        setGeneral(
          applyServerError(error, ['name', 'color'], form.setError, { 'tag.name_taken': 'name' }),
        ),
    })
  })
  const color = useWatch({ control: form.control, name: 'color' })

  return (
    <FormDialog
      title={tag ? 'Edit tag' : 'Add tag'}
      submitLabel={tag ? 'Save' : 'Add tag'}
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
      <div className="flex items-end gap-3">
        <div className="flex-1">
          <FormField
            label="Colour"
            placeholder="#1F8A70"
            hint="Optional."
            error={form.formState.errors.color}
            {...form.register('color')}
          />
        </div>
        <input
          type="color"
          aria-label="Pick a colour"
          className="mb-7 h-11 w-14 shrink-0 cursor-pointer rounded-md border bg-transparent p-1"
          value={COLOR.test(color) ? color : '#000000'}
          onChange={(e) =>
            form.setValue('color', e.target.value.toUpperCase(), { shouldValidate: true })
          }
        />
      </div>
    </FormDialog>
  )
}

function DeleteTagDialog({ tag, onClose }: { tag: Tag; onClose: () => void }) {
  const { client } = useAuthService()
  return (
    <ConfirmDialog
      title="Delete tag?"
      description={`"${tag.name}" will be removed. This cannot be undone.`}
      confirmLabel="Delete tag"
      successMessage="Tag deleted."
      areas={[['tags']]}
      run={() => unwrap(client.DELETE('/api/v1/tags/{id}', { params: { path: { id: tag.id } } }))}
      onClose={onClose}
    />
  )
}
