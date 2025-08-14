namespace CodexIpc {
    export var ServiceMode: "Page" | "Worker";
    export var Service: ICodexPage | IndexService;

    export var OnLoaded: any = () => {
        console.error("OnLoaded base impl");
        return Promise.resolve(true);
    };

    export function GetServiceMode() {
        return ServiceMode;
    }

    export function GetService() {
        return Service;
    }

    export interface ICodexPage {
        setLeftPane(innerHtml?: string): void;
        setRightPane(innerHtml?: string): void;
    }

    interface IIndexService {
        CallAsync(name: string, jsonArgs: string) : Promise<string>;
    }

    interface IIndexServiceReference {
        invokeMethodAsync(methodName: "CallAsync", name: string, jsonArgs: string): Promise<string>;
    }

    export class IndexService {
        Service: IIndexServiceReference;
        ServicePromise = new CodexIpc.PromiseCompletionSource<IIndexServiceReference>();

        StartAsync(service: IIndexServiceReference) {
            console.log("Staring service in browser worker");
            this.Service = service;
            this.ServicePromise.Resolve(service);
            console.log("Finished service in browser worker");

            return true;
        }

        async WaitForStartAsync() {
            await this.ServicePromise.Task;
            return 2920;
        }

        async CallAsync(nameAndArgs: string[]): Promise<string> {
            let service = await this.ServicePromise.Task;
            return await service.invokeMethodAsync("CallAsync", nameAndArgs[0], nameAndArgs[1]);
        }

        async LogAsync(message: string) {
            console.log("Worker recieved message: " + message);
            return 1234;
        }
    }
}