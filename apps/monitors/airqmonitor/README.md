c
# AirQ Adapter

Uses monitor applications with timer triggers to collext AirQ web device and noise data

StoreDevices - [timerTrigger] - Retrieves and Stores the list of devices for user account
Runs on a timer every hour

StoreNoiseLevels - [timerTrigger] Retrieves and Stores sample data for stored devices
    AirQ updates the data every 15 minutes on the quarter hour

StoreAllNoiseLevelsForYesterday - [timerTrigger] Retrieves and Stores sample data for stored devices
    for yesterday, runs at 3 am every day

StoreNoiseLevelsForDate - [httpTrigger]
    Invoke url: https://airqmonitor.azurewebsites.net/api/storenoiselevelsfordate

e.g.  Http GET
https://airqmonitor.azurewebsites.net/api/StoreNoiseLevelsForDate?user_id=<userid>d&user_auth=<token>&date=<YYYY-MM-DD>"

Would store all samples from listed devices on the given date

scripts in AirQMonitorTests have been used to test this.  
  

## Project Setup

Monitor application for collection of sensor data from AirQ/Turnkey API

API Docs https://datacollector.airqweb.com/swagger-ui/

## Shared communications

AirQ resolves email and SMS through the common provider-neutral adapters. Configure `RVT__EMAIL_ENABLED`, `RVT__EMAIL_PROVIDER`, the selected SendGrid or Microsoft Graph settings, and `RVT__SMS_ENABLED` plus TransmitSMS settings as documented in the root README. Docker Compose disables both channels by default so local startup never requires a credential. Durable delivery is at least once; transient failures retry and permanent/configuration failures dead-letter without persisting provider secrets or destinations.


## Build and run locally

In terminal

'''
func start
'''


## Running tests

In directory AirQMonitorTests in terminal run:

'''
dotnet test
'''

## Manual Test

In manual test directory

Contains node scripts which make test requests to the adapter endpoint

Test against local instance (func start)
'''
node test_local_function.js
'''

Test against Azure instance
'''
node test_azure_function.js
'''


## Deploy

In terminal, already logged in to Azure

'''
func monitor applicationapp publish AirQMonitor
'''



### Useful Commands

Debug in Visual Studio Code can sometimes leave the process alive,
then cannot be retarted because port 7071 in use

On Mac get the pid with:
'''
lsof -i :7071
'''
Then kill with:

'''
kill -9 <pid>
'''
