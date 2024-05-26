import { Routes } from '@angular/router';
import { DashboardComponent } from './dashboard/dashboard.component'; // Import the DashboardComponent
import { LoginComponent } from './login/login.component';

export const routes: Routes = [
    { path: 'dashboard', component: DashboardComponent }, // Add the dashboard route here
    {path: 'login', component: LoginComponent},
    { path: '', redirectTo: '/login', pathMatch: 'full' },
    //{ path: '**', component: PageNotFoundComponent } // Add this if you want a 404 page
];
