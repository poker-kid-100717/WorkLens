import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, Subscription, catchError, interval, startWith, switchMap, takeUntil, tap } from 'rxjs';
import { FeedResponse, JobListing, JobMatch } from '../../core/models/models';
import { ApplicationsService } from '../../core/services/applications.service';
import { FeedService } from '../../core/services/feed.service';
import { ResumeService } from '../../core/services/resume.service';

@Component({
  selector: 'app-feed',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './feed.component.html',
  styleUrl: './feed.component.scss'
})
export class FeedComponent implements OnInit, OnDestroy {
  readonly Math = Math;
  feed: FeedResponse | null = null;
  loading = true;
  error: string | null = null;

  search = '';
  remoteOnly = false;
  hideTracked = false;
  page = 1;
  pageSize = 25;

  savingIds = new Set<number>();
  refreshing = false;

  hasResume = false;
  matches = new Map<number, JobMatch>();
  matching = false;

  private readonly pollIntervalMs = 7_000;
  private readonly destroy$ = new Subject<void>();
  private pollSubscription?: Subscription;

  constructor(
    private readonly feedService: FeedService,
    private readonly applicationsService: ApplicationsService,
    private readonly resumeService: ResumeService
  ) {}

  ngOnInit(): void {
    this.resumeService.hasActiveResume()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (has) => {
          this.hasResume = has;
          if (has) this.remoteOnly = true;
          this.startPolling();
        },
        error: () => {
          this.hasResume = false;
          this.startPolling();
        }
      });
  }

  ngOnDestroy(): void {
    this.pollSubscription?.unsubscribe();
    this.destroy$.next();
    this.destroy$.complete();
  }

  startPolling(): void {
    this.pollSubscription?.unsubscribe();
    this.pollSubscription = interval(this.pollIntervalMs)
      .pipe(
        startWith(0),
        switchMap(() => this.fetch()),
        takeUntil(this.destroy$)
      )
      .subscribe();
  }

  private fetch() {
    return this.feedService
      .getFeed({
        search: this.search || undefined,
        remoteOnly: this.remoteOnly || undefined,
        trackedOnly: this.hideTracked ? false : undefined,
        page: this.page,
        pageSize: this.pageSize
      })
      .pipe(
        tap((response) => {
          this.feed = response;
          this.loading = false;
          this.error = null;
          if (this.hasResume) queueMicrotask(() => this.scoreVisibleJobs());
        }),
        catchError(() => {
          this.loading = false;
          this.error = 'Could not load the job feed. The next automatic refresh will retry.';
          return EMPTY;
        })
      );
  }

  scoreVisibleJobs(): void {
    if (!this.feed || this.matching) return;
    const ids = this.feed.items.map((j) => j.id).filter((id) => !this.matches.has(id));
    if (ids.length === 0) return;

    this.matching = true;
    this.resumeService.scoreJobs(ids)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (results) => {
          for (const match of results) this.matches.set(match.jobListingId, match);
          this.matching = false;
        },
        error: () => (this.matching = false)
      });
  }

  matchFor(jobId: number): JobMatch | undefined {
    return this.matches.get(jobId);
  }

  matchClass(score: number): string {
    if (score >= 75) return 'match-high';
    if (score >= 50) return 'match-medium';
    return 'match-low';
  }

  applyFilters(): void {
    this.page = 1;
    this.startPolling();
  }

  changePage(delta: number): void {
    const maxPage = this.feed ? Math.max(1, Math.ceil(this.feed.totalCount / this.pageSize)) : 1;
    this.page = Math.min(Math.max(1, this.page + delta), maxPage);
    this.startPolling();
  }

  refreshNow(): void {
    this.refreshing = true;
    this.feedService.refreshNow()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.refreshing = false;
          this.matches.clear();
          this.startPolling();
        },
        error: () => (this.refreshing = false)
      });
  }

  saveJob(job: JobListing): void {
    if (job.applicationId || this.savingIds.has(job.id)) return;
    this.savingIds.add(job.id);
    this.applicationsService.create({ jobListingId: job.id })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (app) => {
          job.applicationId = app.id;
          job.applicationStatus = app.status;
          this.savingIds.delete(job.id);
        },
        error: () => this.savingIds.delete(job.id)
      });
  }

  secondsAgo(iso: string | null): string {
    if (!iso) return 'never';
    const seconds = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 1000));
    if (seconds < 60) return `${seconds}s ago`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
    return `${Math.floor(seconds / 3600)}h ago`;
  }

  trackByJobId(_: number, job: JobListing): number {
    return job.id;
  }
}
