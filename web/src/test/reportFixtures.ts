import type { components } from '@/api/schema'

import { ISSUE_SITE_ID } from './fixtures'

type DashboardSite = components['schemas']['DashboardSiteResponse']
type DashboardTotals = components['schemas']['DashboardTotalsResponse']
type ComplianceRow = components['schemas']['ComplianceRowResponse']
type ComplianceReport = components['schemas']['ComplianceReportResponse']
type ActivityItem = components['schemas']['ActivityItemResponse']

export const REPORT_SITE_1 = ISSUE_SITE_ID
export const REPORT_SITE_2 = '55555555-0000-0000-0000-000000000002'

/** A site's row on the dashboard: Harbour Cafe with 8 of 10 tasks done, nothing overdue. */
export function dashboardSite(overrides: Partial<DashboardSite> = {}): DashboardSite {
  return {
    siteId: REPORT_SITE_1,
    siteName: 'Harbour Cafe',
    today: '2026-11-07',
    dueToday: 10,
    completed: 8,
    completedOnTime: 8,
    overdue: 0,
    remaining: 2,
    missedYesterday: 0,
    score: 96,
    openIssues: 0,
    overdueIssues: 0,
    openByPriority: {},
    ...overrides,
  }
}

export function dashboardTotals(overrides: Partial<DashboardTotals> = {}): DashboardTotals {
  return {
    dueToday: 10,
    completed: 8,
    completedOnTime: 8,
    overdue: 0,
    remaining: 2,
    missedYesterday: 0,
    score: 96,
    openIssues: 0,
    overdueIssues: 0,
    openByPriority: {},
    ...overrides,
  }
}

export function complianceRow(overrides: Partial<ComplianceRow> = {}): ComplianceRow {
  return {
    key: REPORT_SITE_1,
    name: 'Harbour Cafe',
    due: 20,
    completedOnTime: 19,
    completedLate: 1,
    missed: 0,
    withFailures: 0,
    awaitingApproval: 0,
    score: 95,
    ...overrides,
  }
}

export function complianceReport(
  rows: ComplianceRow[],
  overrides: Partial<ComplianceReport> = {},
): ComplianceReport {
  return {
    from: '2026-10-09',
    to: '2026-11-07',
    groupBy: 'Site',
    rows,
    total: complianceRow({ key: 'total', name: 'Total' }),
    ...overrides,
  }
}

export function activityItem(overrides: Partial<ActivityItem> = {}): ActivityItem {
  return {
    kind: 'task_completed',
    occurredAt: '2026-11-07T09:30:00Z',
    siteId: REPORT_SITE_1,
    siteName: 'Harbour Cafe',
    actorId: 'cccccccc-0000-0000-0000-000000000001',
    actorName: 'Sam Chef',
    subjectId: '99999999-0000-0000-0000-000000000001',
    title: 'Opening checks',
    issueNumber: null,
    ...overrides,
  }
}
