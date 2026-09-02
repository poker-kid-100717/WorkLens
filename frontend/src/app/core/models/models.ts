export interface JobListing {
  id: number;
  source: string;
  title: string;
  company: string;
  location: string | null;
  isRemote: boolean;
  salaryMin: string | null;
  salaryMax: string | null;
  salaryCurrency: string | null;
  tags: string[];
  url: string;
  companyLogoUrl: string | null;
  postedAt: string;
  fetchedAt: string;
  isActive: boolean;
  applicationId: number | null;
  applicationStatus: string | null;
}

export interface FeedSourceStatus {
  source: string;
  lastFetchSucceeded: boolean;
  lastFetchedAt: string | null;
  listingCount: number;
  lastError: string | null;
}

export interface FeedResponse {
  items: JobListing[];
  totalCount: number;
  lastRefreshedAt: string;
  refreshIntervalSeconds: number;
  sourceStatuses: FeedSourceStatus[];
}

export type ApplicationStatus =
  | 'Saved'
  | 'Applied'
  | 'PhoneScreen'
  | 'Interviewing'
  | 'Offer'
  | 'Rejected'
  | 'Withdrawn'
  | 'Ghosted';

export const APPLICATION_STATUSES: ApplicationStatus[] = [
  'Saved',
  'Applied',
  'PhoneScreen',
  'Interviewing',
  'Offer',
  'Rejected',
  'Withdrawn',
  'Ghosted'
];

export interface JobApplication {
  id: number;
  jobListingId: number | null;
  manualEntry: boolean;
  title: string;
  company: string;
  location: string | null;
  url: string | null;
  status: ApplicationStatus;
  savedAt: string;
  appliedAt: string | null;
  lastStatusChangeAt: string | null;
  followUpAt: string | null;
  followUpDismissed: boolean;
  followUpDue: boolean;
  notes: string | null;
  contactName: string | null;
  contactEmail: string | null;
}

export interface CreateApplicationRequest {
  jobListingId?: number | null;
  title?: string;
  company?: string;
  location?: string;
  url?: string;
  notes?: string;
}

export interface UpdateApplicationRequest {
  status?: ApplicationStatus;
  followUpAt?: string | null;
  followUpDismissed?: boolean;
  notes?: string;
  contactName?: string;
  contactEmail?: string;
  statusChangeNote?: string;
}

export interface FunnelStage {
  stage: string;
  count: number;
}

export interface WeeklyCount {
  weekStart: string;
  count: number;
}

export interface CompanyCount {
  company: string;
  count: number;
}

export interface AnalyticsSummary {
  totalApplications: number;
  totalSaved: number;
  totalApplied: number;
  totalInterviewing: number;
  totalOffers: number;
  totalRejected: number;
  responseRatePercent: number;
  interviewRatePercent: number;
  offerRatePercent: number;
  activeApplications: number;
  dueFollowUps: number;
  funnel: FunnelStage[];
  applicationsPerWeek: WeeklyCount[];
  topCompanies: CompanyCount[];
}

export interface SearchProfile {
  id: number;
  name: string;
  keywords: string[];
  remoteOnly: boolean;
  locationFilter: string | null;
  isActive: boolean;
}

export interface SaveSearchProfileRequest {
  name: string;
  keywords: string[];
  remoteOnly: boolean;
  locationFilter?: string | null;
  isActive: boolean;
}

export interface Resume {
  id: number;
  name: string;
  isActive: boolean;
  uploadedAt: string;
  characterCount: number;
}

export interface UploadResumeRequest {
  name: string;
  rawText: string;
}

export interface JobMatch {
  jobListingId: number;
  matchScore: number;
  matchingSkills: string[];
  missingSkills: string[];
  summary: string;
  scoredAt: string;
}

export interface OutlookConnectionStatus {
  isConfigured: boolean;
  isConnected: boolean;
  accountEmail: string | null;
  lastSyncedAt: string | null;
  communicationCount: number;
}

export interface OutlookCommunication {
  messageId: string;
  applicationId: number | null;
  direction: 'Inbound' | 'Outbound' | string;
  subject: string;
  fromName: string | null;
  fromEmail: string | null;
  receivedAt: string;
  preview: string | null;
  webLink: string | null;
  isRead: boolean;
  kind: string;
  matchedCompany: string | null;
  matchedTitle: string | null;
}
