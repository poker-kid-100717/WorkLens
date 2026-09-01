import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateApplicationRequest, JobApplication, UpdateApplicationRequest } from '../models/models';
import { resolveApiBase } from './api-config';

@Injectable({ providedIn: 'root' })
export class ApplicationsService {
  private readonly base = `${resolveApiBase()}/applications`;

  constructor(private http: HttpClient) {}

  getAll(status?: string): Observable<JobApplication[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    return this.http.get<JobApplication[]>(this.base, { params });
  }

  getById(id: number): Observable<JobApplication> {
    return this.http.get<JobApplication>(`${this.base}/${id}`);
  }

  getDueFollowUps(): Observable<JobApplication[]> {
    return this.http.get<JobApplication[]>(`${this.base}/due-followups`);
  }

  create(request: CreateApplicationRequest): Observable<JobApplication> {
    return this.http.post<JobApplication>(this.base, request);
  }

  update(id: number, request: UpdateApplicationRequest): Observable<JobApplication> {
    return this.http.patch<JobApplication>(`${this.base}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
