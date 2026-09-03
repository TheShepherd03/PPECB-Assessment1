import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Category, CategoryLookup, PagedResult } from './models';

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/categories`;

  getPaged(pageNumber: number, pageSize: number, search?: string): Observable<PagedResult<Category>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);

    if (search?.trim()) {
      params = params.set('search', search.trim());
    }

    return this.http.get<PagedResult<Category>>(this.baseUrl, { params });
  }

  getLookup(): Observable<CategoryLookup[]> {
    return this.http.get<CategoryLookup[]>(`${this.baseUrl}/lookup`);
  }

  getById(id: number): Observable<Category> {
    return this.http.get<Category>(`${this.baseUrl}/${id}`);
  }

  create(payload: { name: string; categoryCode: string; isActive: boolean }): Observable<Category> {
    return this.http.post<Category>(this.baseUrl, payload);
  }

  update(
    id: number,
    payload: { name: string; categoryCode: string; isActive: boolean; rowVersion: string | null }
  ): Observable<Category> {
    return this.http.put<Category>(`${this.baseUrl}/${id}`, payload);
  }
}
