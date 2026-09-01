import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, Subscription, interval, startWith, switchMap, takeUntil } from 'rxjs';
import { FeedService } from '../../core/services/feed.service';
import { ApplicationsService } from '../../core/services/applications.service';
import { ResumeService } from '../../core/services/resume.service';
import { FeedResponse, JobListing, JobMatch } from '../../core/models/models';

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

  private readonly POLL_MS = 7000;
  private destroy$ = new Subject<void>();
  private poll$?: Subscription;

  constructor(
    private feedService: FeedService,
    private applicationsService: ApplicationsService,
    private resumeService: ResumeService
  ) {}

  ngOnInit(): void {
    this.resumeService.hasActiveResume().subscribe({
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
    this.destroy$.next();
    this.destroy$.complete();
  }

  startPolling(): void {
    this.poll$?.unsubscribe();
    this.poll$ = interval(this.POLL_MS)
      .pipe(startWith(0), takeUntil(this.destroy$), switchMap(() => this.fetch()))
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
        switchMap((response) => {
          this.feed = response;
          this.loading = false;
          this.error = null;
          if (this.hasResume) queueMicrotask(() => this.scoreVisibleJobs());
          return [response];
        })
      );
  }

  scoreVisibleJobs(): void {
    if (!this.feed || this.matching) return;
    const ids = this.feed.items.map((j) => j.id).filter((id) => !this.matches.has(id));
    if (ids.length === 0) return;

    this.matching = true;
    this.resumeService.scoreJobs(ids).subscribe({
      next: (results) => {
        for (const m of results) this.matches.set(m.jobListingId, m);
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
    this.feedService.refreshNow().subscribe({
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
    this.applicationsService.create({ jobListingId: job.id }).subscribe({
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
