import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';

import { AnalyticsService } from '../../core/services/analytics.service';
import { AnalyticsSummary } from '../../core/models/models';

@Component({
    selector: 'app-analytics',
    imports: [],
    templateUrl: './analytics.component.html',
    changeDetection: ChangeDetectionStrategy.Eager,
    styleUrl: './analytics.component.scss'
})
export class AnalyticsComponent implements OnInit {
  summary: AnalyticsSummary | null = null;
  loading = true;

  constructor(private analyticsService: AnalyticsService) {}

  ngOnInit(): void {
    this.analyticsService.getSummary().subscribe({
      next: (s) => {
        this.summary = s;
        this.loading = false;
      },
      error: () => (this.loading = false)
    });
  }

  maxFunnelCount(): number {
    if (!this.summary) return 1;
    return Math.max(1, ...this.summary.funnel.map((f) => f.count));
  }

  maxWeeklyCount(): number {
    if (!this.summary) return 1;
    return Math.max(1, ...this.summary.applicationsPerWeek.map((w) => w.count));
  }

  maxCompanyCount(): number {
    if (!this.summary) return 1;
    return Math.max(1, ...this.summary.topCompanies.map((c) => c.count));
  }
}
