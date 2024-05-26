import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { MatTabsModule } from '@angular/material/tabs';
import { User, Module, UserRole, UserDetails } from '../models/User';
import { UserService } from '../services/user.service';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, HttpClientModule, MatCardModule, MatListModule, MatTabsModule, MatProgressSpinnerModule,
    MatFormFieldModule, MatSelectModule, ReactiveFormsModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  @Input() username!: string;
  user!: User;
  modules: Module[] = [];
  userEnrolment!: UserDetails;
  role!: string;
  isLoading = false;
  availableModules: Module[] = []; 
  registrationForm!: FormGroup;
  enrolledModules: any[] = [];

  constructor(private userService: UserService, private formBuilder: FormBuilder) {}

  ngOnInit(): void {
    this.fetchUserDetails();
    this.loadModules();
    this.registrationForm = this.formBuilder.group({
      selectedModules: ['', Validators.required]
    });
  }

  ngOnChanges(): void {
    this.fetchUserDetails();
  }

  loadModules(): void {
    this.isLoading = true;
    this.userService.getAllModules().subscribe(
      (modules) => {
        this.availableModules = modules;
        this.isLoading = false;
      },
      (error) => {
        console.error('Error loading modules:', error);
        this.isLoading = false;
      }
    );
  }

  mergeEnrolledModulesWithLecturerMarks(): void {
    this.enrolledModules = this.userEnrolment.EnrolledModules.map((enrolledModule) => {
      const correspondingModule = this.availableModules.find(module => module.Code === enrolledModule.ModuleId);
      if (correspondingModule) {
        return {
          ModuleId: enrolledModule.ModuleId,
          Lecturer: correspondingModule.Lecturer,
          Semester: correspondingModule.Semester,
          SemesterMark: enrolledModule.SemesterMark,
          ExamMark: enrolledModule.ExamMark,
          FinalMark: enrolledModule.FinalMark
        };
      } else {
        return null;
      }
    }).filter(module => module !== null);
  }

  fetchUserDetails(): void {
    if (!this.username) {
      return;
    }
    
    const userId = this.username;
    
    this.userService.getUserEnrolment(userId).subscribe({
      next: (enrolment) => {
        console.log('Fetched modules:', enrolment);
        this.userEnrolment = enrolment;
        this.user = enrolment.User;
        this.role = UserRole[this.userEnrolment.User.Role];
        this.mergeEnrolledModulesWithLecturerMarks();
      },
      error: (err) => {
        console.error('Error fetching user modules:', err);
      }
    });
  }

  registerModules(): void {
    const selectedModules = this.registrationForm.get('selectedModules')!.value;
    console.log('Selected Modules:', selectedModules);
    const userId = this.username; 
    this.isLoading = true;
    this.userService.registerModules(userId, selectedModules)
      .subscribe(
        () => {
          console.log('Modules registered successfully');
          this.isLoading = false;
          this.fetchUserDetails();
        },
        error => {
          console.error('Error registering modules:', error);
          this.isLoading = false;
        }
      );
  }
}
