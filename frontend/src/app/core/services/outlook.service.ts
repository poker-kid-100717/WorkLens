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

  getCommunications(applicationId?: number): Observable<OutlookCommunication[]> {
    let params = new HttpParams();
    if (applicationId !== undefined) params = params.set('applicationId', String(applicationId));
    return this.http.get<OutlookCommunication[]>(`${this.base}/communications`, { params });
  }
}
