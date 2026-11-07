export type ScoreTone = 'good' | 'warn' | 'bad' | 'none'

/** Share of due tasks completed on time: at least 95% is good, 80-95% needs watching, below 80% is poor. */
export const GOOD_SCORE = 95
export const WARN_SCORE = 80

export function scoreTone(score: number | null | undefined): ScoreTone {
  if (score == null) return 'none'
  if (score >= GOOD_SCORE) return 'good'
  if (score >= WARN_SCORE) return 'warn'
  return 'bad'
}

export const TONE_TEXT: Record<ScoreTone, string> = {
  good: 'text-success',
  warn: 'text-warning',
  bad: 'text-destructive',
  none: 'text-muted-foreground',
}

export const TONE_BAR: Record<ScoreTone, string> = {
  good: 'bg-success',
  warn: 'bg-warning',
  bad: 'bg-destructive',
  none: 'bg-muted-foreground',
}

export const TONE_WORD: Record<ScoreTone, string> = {
  good: 'On target',
  warn: 'Needs attention',
  bad: 'Below target',
  none: 'Nothing due',
}

export function formatScore(score: number | null | undefined): string {
  return score == null ? '–' : `${Number(score.toFixed(1))}%`
}
