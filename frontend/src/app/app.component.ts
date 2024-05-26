import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { LoginComponent } from "./login/login.component";
import { DashboardComponent } from "./dashboard/dashboard.component";
import { HttpClientModule } from '@angular/common/http';
import { ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-root',
  standalone: true,
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'],
  imports: [CommonModule, RouterOutlet, LoginComponent, DashboardComponent, HttpClientModule, ReactiveFormsModule]
})
export class AppComponent {
  title = 'frontend';
  username: string = '24123456';
  isAuth = true;

  onAuthChange(event: { isAuth: boolean, username: string }) {
    this.isAuth = event.isAuth;
    this.username = event.username;
  }
}
