import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { SaveSearchProfileRequest, SearchProfile } from '../models/models';
import { resolveApiBase } from './api-config';

@Injectable({ providedIn: 'root' })
export class SearchProfilesService {
  private readonly base = `${resolveApiBase()}/searchprofiles`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<SearchProfile[]> {
    return this.http.get<SearchProfile[]>(this.base);
  }

  create(request: SaveSearchProfileRequest): Observable<SearchProfile> {
    return this.http.post<SearchProfile>(this.base, request);
  }

  update(id: number, request: SaveSearchProfileRequest): Observable<SearchProfile> {
    return this.http.put<SearchProfile>(`${this.base}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
