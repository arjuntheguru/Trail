import type { components } from '@/api/schema'

type Schemas = components['schemas']

export type IssueStatus = Schemas['IssueStatus']
export type IssuePriority = Schemas['IssuePriority']
export type IssueListItem = Schemas['IssueListItemResponse']
export type IssueDetail = Schemas['IssueDetailResponse']
export type IssueEvent = Schemas['IssueEventResponse']
export type IssueSummary = Schemas['IssueSummaryResponse']

export const PRIORITIES: readonly IssuePriority[] = ['Low', 'Medium', 'High', 'Critical']
