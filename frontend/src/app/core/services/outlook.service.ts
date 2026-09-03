import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { OutlookCommunication, OutlookConnectionStatus } from '../models/models';
import { resolveApiBase } from './api-config';

@Injectable({ providedIn: 'root' })
export class OutlookService {
  private readonly base = `${resolveApiBase()}/outlook`;

  constructor(private http: HttpClient) {}

  getStatus(): Observable<OutlookConnectionStatus> {
    return this.http.get<OutlookConnectionStatus>(`${this.base}/status`);
  }

  connect(): void {
    window.location.href = `${this.base}/connect`;
  }

  sync(): Observable<{ added: number }> {
    return this.http.post<{ added: number }>(`${this.base}/sync`, {});
  }

  disconnect(clearCommunications = false): Observable<void> {
    const params = new HttpParams().set('clearCommunications', String(clearCommunications));
    return this.http.post<void>(`${this.base}/disconnect`, {}, { params });
  }

  getCommunications(applicationId?: number): Observable<OutlookCommunication[]> {
    let params = new HttpParams();
    if (applicationId !== undefined) params = params.set('applicationId', String(applicationId));
    return this.http.get<OutlookCommunication[]>(`${this.base}/communications`, { params });
  }

  matchCommunication(messageId: string, applicationId: number | null): Observable<OutlookCommunication> {
    return this.http.patch<OutlookCommunication>(
      `${this.base}/communications/${encodeURIComponent(messageId)}/application`,
      { applicationId }
    );
  }

  trackCommunication(
    messageId: string,
    request: { title: string; company: string; location?: string | null }
  ): Observable<{ applicationId: number }> {
    return this.http.post<{ applicationId: number }>(
      `${this.base}/communications/${encodeURIComponent(messageId)}/track`,
      request
    );
  }
}
