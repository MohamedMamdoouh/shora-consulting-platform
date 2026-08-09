using './main.bicep'

param baseName = 'shora'
param location = 'westeurope'
// Override at deploy time: -p sqlAdminPassword='...' (never commit real passwords)
param sqlAdminPassword = 'ChangeMe-Min12Chars!'
