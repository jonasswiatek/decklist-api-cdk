function handler(event) {
    var request = event.request;

    if (request.uri === '/events') {
        request.uri = "/index.html";
        request.querystring = {}
    }

    return request;
}
