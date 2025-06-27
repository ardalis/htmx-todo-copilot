namespace MyMinimalApi.Extensions;

public static class PageEndpoints
{
    public static IEndpointRouteBuilder MapPageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/", GetHomePage);
        return endpoints;
    }

    private static IResult GetHomePage()
    {
        var html = """
            <!DOCTYPE html>
            <html>
            <head>
                <title>HTMX Todo App</title>
                <script src="https://unpkg.com/htmx.org@1.9.9"></script>
                <script src="https://unpkg.com/@microsoft/signalr@8.0.7/dist/browser/signalr.min.js"></script>
                <link href="/css/site.css" rel="stylesheet" />
            </head>
            <body>
                <div class="container">
                    <h1>Todo Application</h1>
                    <div class="connection-status" id="connection-status">🔄 Connecting...</div>
                    <div id="todo-app" hx-get="/todos" hx-trigger="load"></div>
                </div>
                
                <!-- Toast Container -->
                <div id="toast-container" class="toast-container"></div>
                
                <script>
                    // SignalR Connection
                    const connection = new signalR.HubConnectionBuilder()
                        .withUrl("/todoHub")
                        .build();

                    // Connection status
                    const statusElement = document.getElementById('connection-status');
                    
                    connection.start().then(() => {
                        console.log('SignalR Connected');
                        statusElement.innerHTML = 'Live Updates Active';
                        statusElement.style.color = '#28a745';
                    }).catch(err => {
                        console.error('SignalR Connection Error: ', err);
                        statusElement.innerHTML = '🔴 Connection Failed';
                        statusElement.style.color = '#dc3545';
                    });

                    // Toast notification function
                    function showToast(message, type = 'success') {
                        console.log('Showing toast:', message, type);
                        const container = document.getElementById('toast-container');
                        const toast = document.createElement('div');
                        toast.className = `toast ${type}`;
                        toast.textContent = message;
                        
                        container.appendChild(toast);
                        
                        setTimeout(() => toast.classList.add('show'), 100);
                        
                        setTimeout(() => {
                            toast.classList.remove('show');
                            setTimeout(() => {
                                if (container.contains(toast)) {
                                    container.removeChild(toast);
                                }
                            }, 300);
                        }, 3000);
                    }

                    // SignalR Event Handlers
                    connection.on("TodoAdded", function (data) {
                        console.log('Todo added via SignalR:', data);
                        
                        // Add the new todo to the list if it doesn't exist
                        if (!document.getElementById(`todo-${data.id}`)) {
                            const todoList = document.getElementById('todo-list');
                            if (todoList) {
                                todoList.insertAdjacentHTML('beforeend', data.html);
                            }
                        }
                        
                        showToast(`"${data.title}" was added by another user!`, 'info');
                    });

                    connection.on("TodoToggled", function (data) {
                        console.log('Todo toggled via SignalR:', data);
                        
                        const todoElement = document.getElementById(`todo-${data.id}`);
                        if (todoElement) {
                            todoElement.outerHTML = data.html;
                        }
                        
                        const status = data.isCompleted ? 'completed' : 'reopened';
                        showToast(`"${data.title}" was ${status} by another user!`, 'info');
                    });

                    connection.on("TodoDeleted", function (data) {
                        console.log('Todo deleted via SignalR:', data);
                        
                        const todoElement = document.getElementById(`todo-${data.id}`);
                        if (todoElement) {
                            todoElement.remove();
                        }
                        
                        showToast(`"${data.title}" was deleted by another user!`, 'info');
                    });

                    // Test function
                    window.testToast = () => showToast('Test notification!', 'success');
                    
                    // HTMX event handlers for local actions
                    document.body.addEventListener('htmx:afterRequest', function(event) {
                        console.log('HTMX afterRequest event:', event.detail);
                        
                        const xhr = event.detail.xhr;
                        const requestConfig = event.detail.requestConfig;
                        
                        if (xhr.status === 200 && requestConfig) {
                            const method = requestConfig.verb.toUpperCase();
                            const url = requestConfig.path;
                            
                            console.log('Request details:', method, url);
                            
                            if (method === 'POST' && url === '/todos') {
                                showToast('Todo item added successfully!', 'success');
                            } else if (method === 'DELETE' && url.startsWith('/todos/')) {
                                showToast('Todo item deleted!', 'success');
                            } else if (method === 'PUT' && url.includes('/toggle')) {
                                showToast('Todo item updated!', 'success');
                            }
                        }
                    });
                    
                    // Clear form after successful POST
                    document.body.addEventListener('htmx:afterRequest', function(event) {
                        const xhr = event.detail.xhr;
                        const requestConfig = event.detail.requestConfig;
                        
                        if (xhr.status === 200 && 
                            requestConfig.verb === 'POST' && 
                            requestConfig.path === '/todos') {
                            const form = event.detail.elt;
                            if (form.tagName === 'FORM') {
                                form.reset();
                            }
                        }
                    });
                </script>
            </body>
            </html>
        """;

        return Results.Content(html, "text/html");
    }
}
