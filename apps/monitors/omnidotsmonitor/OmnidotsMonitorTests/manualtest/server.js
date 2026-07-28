
const port = Number(process.argv[2]);
//const http = require("http");

//const server = http.createServer((request, response) => {
//  console.log(request);
//  response.end();
//});


console.log("Starting server on port "+port);
//server.listen(port);

const express = require("express");
const myParser = require("body-parser");
const app = express();

app.use(myParser.urlencoded({ extended: true }));
app.post("/", function (request, response) {
    const date_time = new Date();

    console.log("time=" + date_time);
    console.log("hdrs=" + JSON.stringify(request.headers));
    console.log("req="+request);
    console.log("body="+request.body); 
    //console.log("Sreq=" + JSON.stringify(request));
    console.log("Sbody=" + JSON.stringify(request.body)); 

});

app.listen(port);
