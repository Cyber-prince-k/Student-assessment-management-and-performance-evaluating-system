# Student Assessment Management and Performance Evaluation System

A Windows desktop application for managing assessments, administering quizzes, evaluating student performance, and supporting supervised assessment sessions.

## Features

- Account creation and role-based workflows for administrators, teachers, and students.
- Assessment, question, class, subject, and user-role management.
- Timed assessments with multiple-choice, structured, and essay-style questions.
- SQLite-backed local data storage.
- Performance reports and assessment history.
- Email notifications through configurable SMTP settings.
- Camera-based proctoring with face and eye detection using OpenCvSharp.
- Proctoring status updates, violation tracking, session termination, and assistance requests.

## Technology

- C# Windows Forms
- .NET Framework 4.7.2
- Visual Studio solution and MSBuild project
- SQLite and Entity Framework 6
- Guna.UI2.WinForms
- OpenCvSharp 4 with the Windows runtime
- iTextSharp for report generation

## Requirements

- Windows
- Visual Studio with .NET Framework 4.7.2 developer tools
- A camera for proctored assessments
- Git, if cloning the repository

## Getting Started

1. Clone the repository.
2. Open `Assessment management and performance evaluation.sln` in Visual Studio.
3. Restore the NuGet packages listed in `packages.config` if Visual Studio does not restore them automatically.
4. Build the solution.
5. Start the application from Visual Studio.

The application uses `assessment.db` as a local SQLite database. The database is created or updated by the application as needed and is intentionally excluded from Git.

## Email Configuration

SMTP settings are read from `App.config`. Configure `SmtpHost`, `SmtpPort`, `SenderEmail`, `SenderAppPassword`, and `EnableSsl` for the local environment. Do not commit real passwords, API keys, or app passwords. For Gmail, use a dedicated account and an app password rather than a normal account password.

## Proctoring Assets

The Haar cascade files required by the proctoring engine are stored in the project's `Models` directory and are copied to the build output by the project configuration. A working camera and appropriate Windows camera permissions are required for live proctoring.

## Repository Layout

```text
Assessment management and performance evaluation.sln
Assessment management and performance evaluation/
  *.cs                         Application and domain code
  Models/                      OpenCV Haar cascade models
  *.resx                       Windows Forms resources
packages/                      Restored .NET Framework dependencies
```

Build output, IDE metadata, local databases, and other machine-generated files are excluded by `.gitignore`.

## Security Note

This application handles assessment results, account information, camera data, and email configuration. Use test data during development, protect local database files, and review access controls before deploying it in a production environment.