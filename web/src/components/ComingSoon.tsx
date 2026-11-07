import { Construction } from 'lucide-react'

export function ComingSoon({ title }: { title: string }) {
  return (
    <section className="flex flex-col items-center gap-3 py-16 text-center">
      <Construction className="size-10 text-muted-foreground" aria-hidden />
      <h1 className="text-2xl font-semibold">{title}</h1>
      <p className="text-muted-foreground">Coming soon.</p>
    </section>
  )
}
