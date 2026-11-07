import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useRouter } from '@tanstack/react-router'
import { Check, ChevronsUpDown } from 'lucide-react'
import { toast } from 'sonner'

import { errorMessage } from '@/api/problem'
import { useAuthService, useSession } from '@/auth/context'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

/** Shows the active organization; becomes a switcher when the user belongs to more than one. */
export function OrganizationSwitcher({ compact = false }: { compact?: boolean }) {
  const { organization, organizations } = useSession()
  const auth = useAuthService()
  const queryClient = useQueryClient()
  const router = useRouter()

  const switchTo = useMutation({
    mutationFn: (organizationId: string) => auth.switchOrganization(organizationId),
    onSuccess: () => {
      // Everything cached belongs to the previous organization.
      queryClient.clear()
      router.history.push('/today')
      toast.success('Switched organization.')
    },
    onError: (error) => toast.error(errorMessage(error)),
  })

  const name = organization?.name ?? 'No organization'

  if (organizations.length <= 1) {
    return (
      <div
        className={
          compact ? 'truncate px-2 text-base font-semibold' : 'truncate px-2 font-semibold'
        }
      >
        {name}
      </div>
    )
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          className="min-w-0 justify-between"
          aria-label={`Organization: ${name}. Switch organization`}
          disabled={switchTo.isPending}
        >
          <span className="truncate">{name}</span>
          <ChevronsUpDown className="opacity-60" aria-hidden />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-64">
        <DropdownMenuLabel>Organizations</DropdownMenuLabel>
        <DropdownMenuSeparator />
        {organizations.map((o) => (
          <DropdownMenuItem
            key={o.id}
            className="min-h-11"
            disabled={o.id === organization?.id}
            onSelect={() => switchTo.mutate(o.id)}
          >
            <span className="flex-1 truncate">{o.name}</span>
            {o.id === organization?.id && <Check aria-label="Current" />}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
