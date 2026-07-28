const https = require('http');
//const https = require('https');

//const x = {
//    "serialid": "23423",
////    "webhook"": "https://cbce-2a00-23c7-f0e-9801-d157-d335-905b-c619.ngrok-free.app/api/Webhook",
//    "webhook": "https://omnidotsmonitor.azurewebsites.net/api/webhook",
//    "measuring_type": "Limited",
//    "vibration_type": "Continuous",
//    "trace_save_level": "10.0",
//    "trace_pre_trigger": "3.0",
//    "trace_post_trigger": "3.0",
//    "vector_enabled": "On",
//    "flat_level": "10"
//};


// office dust monitor id is
// SELECT TOP(1000) * FROM[dbo].[MonitorsList] WHERE TypeOfMonitor = 2 and SerialId = '23423'

const omnidotsTestSecret = process.env.OMNIDOTS_TEST_SECRET;

if (!omnidotsTestSecret) {
    throw new Error('OMNIDOTS_TEST_SECRET environment variable is required.');
}

const postData = JSON.stringify({
    'serialid': "23423",
//    'webhook': 'https://cbce-2a00-23c7-f0e-9801-d157-d335-905b-c619.ngrok-free.app/api/Webhook',
    'webhook': 'https://omnidotsmonitor.azurewebsites.net/api/webhook',
    'measuring_type': 'Limited',
    'vibration_type': 'Continuous',
    'vector_enabled' : 'On',
    'trace_save_level': 10.0,
    'trace_pre_trigger': 3.0,
    'trace_post_trigger': 3.0,
    'vector_enabled': 'On',
    'flat_level': 10.0,
    'secret': omnidotsTestSecret
});
//  https://omnidotsmonitor.azurewebsites.net/api/configuremeasuringpoint
const options = {
    hostname: '127.0.0.1',
//    hostname: 'omnidotsmonitor.azurewebsites.net',
    port: 7071,
    path: '/api/configuremeasuringpoint?mpid=23423',
    method: 'POST',
    headers: {
        'Content-Type': 'application/json',
        'Content-Length': postData.length
    }
};

const req = https.request(options, (res) => {
    console.log('statusCode:', res.statusCode);
    console.log('headers:', res.headers);

    res.on('data', (d) => {
        process.stdout.write(d);
    });
});

req.on('error', (e) => {
    console.error(e);
});

req.write(postData);
req.end();
