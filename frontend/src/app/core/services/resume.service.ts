import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { JobMatch, Resume, UploadResumeRequest } from '../models/models';
import { resolveApiBase } from './api-config';

@Injectable({ providedIn: 'root' })
export class ResumeService {
  private readonly resumeBase = `${resolveApiBase()}/resume`;
  private readonly matchBase = `${resolveApiBase()}/match`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<Resume[]> {
    return this.http.get<Resume[]>(this.resumeBase);
  }

  getActive(): Observable<Resume> {
    return this.http.get<Resume>(`${this.resumeBase}/active`);
  }

  upload(request: UploadResumeRequest): Observable<Resume> {
    return this.http.post<Resume>(this.resumeBase, request);
  }

  hasActiveResume(): Observable<boolean> {
    return this.http.get<boolean>(`${this.matchBase}/has-resume`);
  }

  scoreJobs(jobListingIds: number[]): Observable<JobMatch[]> {
    return this.http.post<JobMatch[]>(`${this.matchBase}/score`, { jobListingIds });
  }
}
