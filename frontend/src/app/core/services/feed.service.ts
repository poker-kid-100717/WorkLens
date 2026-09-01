import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { FeedResponse, JobListing } from '../models/models';
import { resolveApiBase } from './api-config';

@Injectable({ providedIn: 'root' })
export class FeedService {
  private readonly base = `${resolveApiBase()}/feed`;

  constructor(private http: HttpClient) {}

  getFeed(opts: {
    search?: string;
    remoteOnly?: boolean;
    trackedOnly?: boolean;
    page?: number;
    pageSize?: number;
  }): Observable<FeedResponse> {
    let params = new HttpParams();
    if (opts.search) params = params.set('search', opts.search);
    if (opts.remoteOnly !== undefined) params = params.set('remoteOnly', String(opts.remoteOnly));
    if (opts.trackedOnly !== undefined) params = params.set('trackedOnly', String(opts.trackedOnly));
    params = params.set('page', String(opts.page ?? 1));
    params = params.set('pageSize', String(opts.pageSize ?? 25));

    return this.http.get<FeedResponse>(this.base, { params });
  }

  getById(id: number): Observable<JobListing> {
    return this.http.get<JobListing>(`${this.base}/${id}`);
  }

  refreshNow(): Observable<void> {
    return this.http.post<void>(`${this.base}/refresh`, {});
  }
}
