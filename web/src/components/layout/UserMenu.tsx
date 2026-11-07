import { useMutation } from '@tanstack/react-query'
import { useRouter } from '@tanstack/react-router'
import { LogOut, MonitorOff } from 'lucide-react'
import { toast } from 'sonner'

import { errorMessage } from '@/api/problem'
import { useAuthService, useSession } from '@/auth/context'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

export function UserMenu({ compact = false }: { compact?: boolean }) {
  const { user, organization } = useSession()
  const auth = useAuthService()
  const router = useRouter()
  const fullName = `${user.firstName} ${user.lastName}`.trim()
  const initials = `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase() || '?'

  const signOut = useMutation({
    mutationFn: () => auth.logout(),
    onSuccess: () => router.history.push('/login'),
  })

  const signOutEverywhere = useMutation({
    mutationFn: () => auth.logoutEverywhere(),
    onSuccess: () => router.history.push('/login'),
    onError: (error) => toast.error(errorMessage(error)),
  })

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          className={compact ? 'h-11 px-1' : 'h-auto justify-start gap-3 px-2 py-2 text-left'}
          aria-label={`Account menu for ${fullName}`}
        >
          <Avatar>
            <AvatarFallback>{initials}</AvatarFallback>
          </Avatar>
          {!compact && (
            <span className="min-w-0">
              <span className="block truncate text-sm font-medium">{fullName}</span>
              <span className="block truncate text-xs text-muted-foreground">
                {organization?.role ?? user.email}
              </span>
            </span>
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-64">
        <DropdownMenuLabel>
          <div className="truncate">{fullName}</div>
          <div className="truncate text-xs font-normal text-muted-foreground">{user.email}</div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem className="min-h-11" onSelect={() => signOut.mutate()}>
          <LogOut aria-hidden /> Sign out
        </DropdownMenuItem>
        <DropdownMenuItem
          className="min-h-11"
          onSelect={() => {
            if (window.confirm('Sign out of Trail on every device?')) signOutEverywhere.mutate()
          }}
        >
          <MonitorOff aria-hidden /> Sign out everywhere
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
