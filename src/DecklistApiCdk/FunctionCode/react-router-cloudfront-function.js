function handler(event) {
    const request = event.request;
    
    if (request.uri.startsWith('/e/'))
    {
        request.uri = "/index.html";
        request.querystring = {}
    }

    return request;
}
