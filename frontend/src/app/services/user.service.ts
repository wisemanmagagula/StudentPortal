// src/app/services/user.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { Module, User, UserDetails } from '../models/User';

@Injectable({
  providedIn: 'root'
})
export class UserService {

  private apiUrl = 'http://localhost:7071/api'; // Replace 'your-api-url' with your actual API URL

  constructor(private http: HttpClient) { }

  login(username: string, password: string): Observable<boolean> {
    return this.http.post<any>(`${this.apiUrl}/Authenticate`, { username, password })
      .pipe(
        map(response => {
          localStorage.setItem('token', response.token);
          return true;
        }),
        catchError(error => {
          console.error('Error logging in:', error);
          return of(false);
        })
      );
  }

  register(username: string, password: string): Observable<boolean> {
    return this.http.post<any>(`${this.apiUrl}/register`, { username, password })
      .pipe(
        map(response => {
          return true;
        }),
        catchError(error => {
          console.error('Error registering:', error);
          return of(false);
        })
      );
  }

  logout(): void {
    localStorage.removeItem('token');
  }

  isAuthenticated(): boolean {
    return !!localStorage.getItem('token');
  }

  getUserDetails(userId: string): Observable<User> {
    return this.http.get<User>(`${this.apiUrl}/users/${userId}`);
  }

  getUserEnrolment(userId: string): Observable<UserDetails> {
    return this.http.get<UserDetails>(`${this.apiUrl}/users/${userId}/enrolment`);
  }

  getAllModules(): Observable<Module[]> {
    return this.http.get<Module[]>(`${this.apiUrl}/modules`);
  }

  registerModules(userId: string, moduleIds: string[]): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/users/${userId}/enrol-modules`, { moduleIds });
  }

  updateUserPassword(userId: string, newPassword: string): Observable<boolean> {
    return this.http.post<any>(`${this.apiUrl}/users/${userId}/update-password`, { newPassword })
      .pipe(
        map(response => {
          return true;
        }),
        catchError(error => {
          console.error('Error updating password:', error);
          return of(false);
        })
      );
  }
}
