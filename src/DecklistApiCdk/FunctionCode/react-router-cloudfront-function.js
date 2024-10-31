function handler(event) {
    const request = event.request;
    
    if (request.uri.startsWith('/events') ||
        request.uri.startsWith('/new-event'))
    {
        request.uri = "/index.html";
        request.querystring = {}
    }

    return request;
}
