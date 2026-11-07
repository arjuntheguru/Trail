import { cn } from '@/lib/utils'

import { formatScore, scoreTone, TONE_BAR, TONE_TEXT, TONE_WORD } from './scoreTone'

/** The percentage in the tone's colour, with a bar. The tone is also spelled out for screen readers. */
export function Score({ score, className }: { score: number | null; className?: string }) {
  const tone = scoreTone(score)
  return (
    <span
      className={cn('inline-flex items-center gap-2', className)}
      data-testid="score"
      data-tone={tone}
    >
      <span className={cn('font-semibold tabular-nums', TONE_TEXT[tone])}>
        {formatScore(score)}
        <span className="sr-only"> ({TONE_WORD[tone]})</span>
      </span>
      {score != null && (
        <span className="h-2 w-16 overflow-hidden rounded-full bg-muted" aria-hidden>
          <span
            className={cn('block h-full rounded-full', TONE_BAR[tone])}
            style={{ width: `${Math.min(100, Math.max(0, score))}%` }}
          />
        </span>
      )}
    </span>
  )
}
