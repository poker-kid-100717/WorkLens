import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AnalyticsService } from '../../core/services/analytics.service';
import { AnalyticsSummary } from '../../core/models/models';

@Component({
  selector: 'app-analytics',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './analytics.component.html',
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
