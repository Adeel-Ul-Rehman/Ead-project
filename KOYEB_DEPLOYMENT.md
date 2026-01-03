# Koyeb Deployment Guide

## Step 1: Push to GitHub

Run these commands:
```bash
gh auth setup-git
git add .
git commit -m "Add Koyeb configuration"
git push -u origin main
```

## Step 2: Create Koyeb Account

1. Go to: https://app.koyeb.com/auth/signup
2. Sign up with your GitHub account (easiest)
3. No credit card required!

## Step 3: Create PostgreSQL Database

1. In Koyeb dashboard, click "Create Database"
2. Select "PostgreSQL"
3. Choose "Free" plan
4. Name it: "attendance-db"
5. Copy the connection string

## Step 4: Deploy Application

1. Click "Create App"
2. Select "GitHub"
3. Choose repository: "Adeel-Ul-Rehman/Ead-project"
4. Builder: Select "Dockerfile"
5. Add Environment Variable:
   - Key: `ConnectionStrings__DefaultConnection`
   - Value: <paste-your-postgresql-connection-string>
6. Click "Deploy"

## Step 5: Update App Settings

After deployment, get your app URL (e.g., `https://attendance-xyz.koyeb.app`)

Update in your code and redeploy:
- File: `attendenceProject/appsettings.Production.json`
- Update: `AppSettings.LoginUrl` with your Koyeb URL

## Step 6: Seed Database

Visit: `https://your-app.koyeb.app/Admin/DatabaseSeeding`
Click "Start Database Seeding"

## Done! 🎉

Your app is live at: `https://your-app.koyeb.app`

Login with:
- Username: admin
- Password: Admin@123
