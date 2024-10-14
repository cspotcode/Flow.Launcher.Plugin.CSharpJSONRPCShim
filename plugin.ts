// Deno exe

import { readLines } from "https://deno.land/std/io/mod.ts";

interface Message {
  Id: number;
  Method?: string;
  Params?: any;
  Result?: any;
}

interface Query {
  // Define the structure of a Query object
  search: string;
  // Add other properties as needed
}

interface Result {
  Title: string;
  Subtitle: string;
  // Add other properties as needed
}

class FlowLauncherPlugin {
  private nextId = 1;

  async run() {
    for await (const line of readLines(Deno.stdin)) {
      try {
        const message: Message = JSON.parse(line);
        await this.handleMessage(message);
      } catch (error) {
        console.error("Error processing message:", error);
      }
    }
  }

  private async handleMessage(message: Message) {
    if (message.Method) {
      // This is a method call from the C# shim
      switch (message.Method) {
        case "Init":
          await this.handleInit(message);
          break;
        case "Query":
          await this.handleQuery(message);
          break;
        default:
          console.error(`Unknown method: ${message.Method}`);
      }
    } else {
      // This is a response to our callback
      this.handleResponse(message);
    }
  }

  private async handleInit(message: Message) {
    console.error("Plugin initialized"); // Log to stderr for debugging
    this.sendResponse(message.Id, { status: "initialized" });
  }

  private async handleQuery(message: Message) {
    const query = message.Params as Query;
    const results: Result[] = await this.performQuery(query);
    this.sendResponse(message.Id, results);
  }

  private async performQuery(query: Query): Promise<Result[]> {
    // Implement your query logic here
    // This is a placeholder implementation
    return [
      {
        Title: `Result for "${query.search}"`,
        Subtitle: "This is a placeholder result",
      },
    ];
  }

  private handleResponse(message: Message) {
    // Handle responses to our callbacks here
    console.error(`Received response for ID ${message.Id}`);
  }

  private sendResponse(id: number, result: any) {
    const response: Message = { Id: id, Result: result };
    console.log(JSON.stringify(response));
  }

  private sendCallback(method: string, params: any) {
    const callbackMessage: Message = {
      Id: this.nextId++,
      Method: method,
      Params: params,
    };
    console.log(JSON.stringify(callbackMessage));
    // Note: In a real implementation, you might want to wait for the response
  }

  // Example method to change the query
  changeQuery(newQuery: string) {
    this.sendCallback("ChangeQuery", { query: newQuery });
  }
}

// Run the plugin
new FlowLauncherPlugin().run();
