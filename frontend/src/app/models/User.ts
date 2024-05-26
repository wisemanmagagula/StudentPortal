export interface User {
  Id: string;
  Username: string;
  Password: string;
  Name: string;
  Surname: string;
  Role: UserRole;
}

export enum UserRole {
  Student,
  Lecturer,
  Admin
}

export interface Module {
  Id: string;
  Name: string;
  Code: string;
  Lecturer: string;
  Semester: number;
}

export interface UserModule{
  Id: string;
  ModuleId:string;
  SemesterMark: string;
  ExamMark: string;
  FinalMark: string;
  IsRegistered: boolean;
}

export interface UserDetails{
  User: User;
  EnrolledModules: UserModule[];
}