import { Component, OnDestroy, OnInit, ChangeDetectionStrategy } from '@angular/core';

import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Subscription, interval, startWith, switchMap } from 'rxjs';
import { ApplicationsService } from './core/services/applications.service';
import { JobApplication } from './core/models/models';

@Component({
    selector: 'app-root',
    imports: [RouterOutlet, RouterLink, RouterLinkActive],
    templateUrl: './app.component.html',
    changeDetection: ChangeDetectionStrategy.Eager,
    styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  dueFollowUps: JobApplication[] = [];
  showReminders = false;
  private sub?: Subscription;

  // Reminders are cheap to check and don't hit upstream feeds, so this polls a bit
  // slower than the feed itself — every 30s is plenty for "you have N follow-ups due".
  private readonly reminderPollMs = 30_000;

  constructor(private applicationsService: ApplicationsService) {}

  ngOnInit(): void {
    this.sub = interval(this.reminderPollMs)
      .pipe(
        startWith(0),
        switchMap(() => this.applicationsService.getDueFollowUps())
      )
      .subscribe({
        next: (apps) => (this.dueFollowUps = apps),
        error: () => (this.dueFollowUps = [])
      });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  toggleReminders(): void {
    this.showReminders = !this.showReminders;
  }
}
