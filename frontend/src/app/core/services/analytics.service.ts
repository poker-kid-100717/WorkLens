import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AnalyticsSummary } from '../models/models';
import { resolveApiBase } from './api-config';

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private readonly base = `${resolveApiBase()}/analytics`;

  constructor(private http: HttpClient) {}

  getSummary(): Observable<AnalyticsSummary> {
    return this.http.get<AnalyticsSummary>(`${this.base}/summary`);
  }
}
