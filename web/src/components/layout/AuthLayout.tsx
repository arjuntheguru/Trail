import type { ReactNode } from 'react'

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export function AuthLayout({
  title,
  description,
  children,
  footer,
}: {
  title: string
  description?: ReactNode
  children: ReactNode
  footer?: ReactNode
}) {
  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-6 px-4 py-10">
      <div className="flex items-center gap-2 text-2xl font-semibold tracking-tight">
        <span className="grid size-9 place-items-center rounded-lg bg-primary text-primary-foreground">
          T
        </span>
        Trail
      </div>
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle className="text-xl" role="heading" aria-level={1}>
            {title}
          </CardTitle>
          {description && <CardDescription>{description}</CardDescription>}
        </CardHeader>
        <CardContent className="flex flex-col gap-5">{children}</CardContent>
      </Card>
      {footer && <p className="text-center text-sm text-muted-foreground">{footer}</p>}
    </main>
  )
}
