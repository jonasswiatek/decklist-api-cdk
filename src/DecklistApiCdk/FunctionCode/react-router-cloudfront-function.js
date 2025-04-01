function handler(event) {
    const request = event.request;
    
    if (request.uri.startsWith('/e/')) {
        request.uri = "/index.html";
        request.querystring = {}
    }
    else if (request.uri.startsWith('/help')) {
        request.uri = "/index.html";
        request.querystring = {}
    }
    else if (request.uri.startsWith('/multi')) {
        request.uri = "/index.html";
        request.querystring = {}
    }

    return request;
}
