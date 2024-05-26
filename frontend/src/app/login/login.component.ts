import { Component, Input, OnInit, Output, EventEmitter } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { UserService } from '../services/user.service';

@Component({
    selector: 'app-login',
    standalone: true,
    imports: [
        ReactiveFormsModule,
        CommonModule,
        HttpClientModule,
        MatInputModule,
        MatButtonModule,
        MatCardModule,
        MatProgressSpinnerModule
    ],
    templateUrl: './login.component.html',
    styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit {
    loginForm!: FormGroup;
    loading = false;
    submitted = false;
    error = '';
    returnUrl!: string;
    @Input() isAuth = false;
    @Output() authChange = new EventEmitter<{ isAuth: boolean, username: string }>();

    constructor(
        private formBuilder: FormBuilder,
        private router: Router,
        private userService: UserService
    ) { }

    ngOnInit() {
        this.loginForm = this.formBuilder.group({
            username: ['', Validators.required],
            password: ['', Validators.required]
        });

        this.returnUrl = '/dashboard';
    }

    get f() { return this.loginForm.controls; }

    onSubmit() {
        this.submitted = true;
        this.error = '';

        if (this.loginForm.invalid) {
            return;
        }

        this.loading = true;
        this.userService.login(this.f['username'].value, this.f['password'].value)
            .subscribe(
                () => {
                    this.router.navigate([this.returnUrl]);
                    this.loading = false;
                    this.isAuth = true;
                    this.authChange.emit({ isAuth: true, username: this.f['username'].value });
                },
                error => {
                    this.error = error;
                    this.loading = false;
                }
            );
    }
}
