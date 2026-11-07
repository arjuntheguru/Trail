import { Send } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'

import { unwrap } from '@/api/unwrap'
import { useAuthService, usePermission } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { FormAlert } from '@/components/FormField'
import { PhotoPicker } from '@/components/Photos'
import { useUploadTracker } from '@/components/useUploadTracker'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'

import type { IssueDetail } from './types'
import { splitError, useIssueAction } from './useIssueAction'

/** Comments are allowed in every status, so a closed issue can still collect notes. */
export function CommentBox({ issue }: { issue: IssueDetail }) {
  const canComment = usePermission(Permissions.Issues.Raise)
  const { client } = useAuthService()
  const uploads = useUploadTracker()
  const [text, setText] = useState('')
  const [photoIds, setPhotoIds] = useState<string[]>([])
  const [empty, setEmpty] = useState(false)
  const { mutation, error } = useIssueAction(
    issue.id,
    () =>
      unwrap(
        client.POST('/api/v1/issues/{id}/comments', {
          params: { path: { id: issue.id } },
          body: { text: text.trim() || null, photoIds },
        }),
      ),
    () => {
      setText('')
      setPhotoIds([])
      toast.success('Comment added.')
    },
  )
  if (!canComment) return null

  const problems = splitError(error, 'text')
  const fieldError = empty ? 'Write a comment or add a photo.' : problems.field
  return (
    <section aria-label="New comment" className="flex flex-col gap-3 border-t pt-4">
      <Label htmlFor="comment-text" className="text-base font-medium">
        Add a comment
      </Label>
      <Textarea
        id="comment-text"
        value={text}
        maxLength={2000}
        aria-invalid={fieldError ? true : undefined}
        onChange={(e) => {
          setText(e.target.value)
          setEmpty(false)
        }}
        className="min-h-24"
      />
      {fieldError && (
        <p role="alert" className="text-sm text-destructive">
          {fieldError}
        </p>
      )}
      <PhotoPicker
        label="Comment photo"
        siteId={issue.siteId}
        value={photoIds}
        onChange={setPhotoIds}
        trackUpload={uploads.track}
      />
      <FormAlert messages={problems.general} />
      <Button
        size="lg"
        disabled={mutation.isPending || uploads.pending > 0}
        onClick={() => (text.trim() || photoIds.length > 0 ? mutation.mutate() : setEmpty(true))}
      >
        <Send aria-hidden />
        Post comment
      </Button>
    </section>
  )
}
