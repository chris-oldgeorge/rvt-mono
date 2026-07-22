
const port = Number(process.argv[2]);
//const http = require("http");

//const server = http.createServer((request, response) => {
//  console.log(request);
//  response.end();
//});


console.log("Starting server on port "+port);
//server.listen(port);

var express = require("express");
var myParser = require("body-parser");
var app = express();

app.use(myParser.urlencoded({ extended: true }));
app.post("/", function (request, response) {
    var date_time = new Date();

    console.log("time=" + date_time);
    console.log("hdrs=" + JSON.stringify(request.headers));
    console.log("req="+request);
    console.log("body="+request.body); 
    //console.log("Sreq=" + JSON.stringify(request));
    console.log("Sbody=" + JSON.stringify(request.body)); 

});

app.listen(port);