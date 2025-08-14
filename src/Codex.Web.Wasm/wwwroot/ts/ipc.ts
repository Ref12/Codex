namespace CodexIpc {
    export class PromiseCompletionSource<T> {
        Resolve: (value: T) => void;
        Reject: (reason:any) => void;
        Task: Promise<T>;
        constructor() {
            var self = this;
            this.Task = new Promise<T>(function (resolve, reject) {
                self.Resolve = resolve;
                self.Reject = reject;
            });
        }
    }

    interface IPromiseResult {
        resolve?: any;
        reject?: any;
    }

    var UnknownGlobal = globalThis as unknown;
    var WorkerGlobalScope = UnknownGlobal as WorkerGlobalScope;
    var SharedWorkerGlobalScope = UnknownGlobal as SharedWorkerGlobalScope;
    var DedicatedWorkerGlobalScope = UnknownGlobal as DedicatedWorkerGlobalScope;

    interface IMessage {
        id: number,
        method: string,
        args?: any,
        callId?: number,
        returnCallId?: number
    }

    interface WorkerGlobalScope {
        importScripts(...urls: (string | URL)[]): void;
    }

    interface SharedWorkerGlobalScope extends WorkerGlobalScope {
        onconnect: ((this: SharedWorkerGlobalScope, ev: MessageEvent) => any) | null;
    }

    interface DedicatedWorkerGlobalScope extends WorkerGlobalScope, IPort {
    }

    interface IPort {
        onmessage: ((this: IPort, ev: MessageEvent) => any) | null;
        onmessageerror: ((this: IPort, ev: MessageEvent) => any) | null;

        postMessage(message: IMessage, transfer?: Transferable[]): void;
    }

    enum WellKnownCallIds {
        Disconnect = -1
    }

    export class WorkerClientBase<T> {
        readonly Id: number;
        readonly Server: IpcServer;
        readonly Target: T;

        constructor(server: IpcServer, port: IPort) {
            this.Id = server.connect(port);

            this.Server = server;
            var proxy = new Proxy(this, {
                get: this.getFunction,
                has: function (target, key) {
                    return true;
                }
            });

            this.Target = proxy as unknown as T;
        }

        getFunction(target: WorkerClientBase<T>, name: string, receiver): any {
            if (name === "then") {
                return undefined;
            }

            return function (arg) {
                var message: IMessage = {
                    args: arg,
                    id: target.Id,
                    method: name,
                };
                return target.Server.callClientAsync(message);
            }
        }

        close() {
            this.Server.disconnect(this.Id);
        }
    }

    export class SharedWorkerClient<T> extends WorkerClientBase<T> {

        readonly Worker: SharedWorker;

        constructor(server: IpcServer, startupScript: string) {
            var worker = new SharedWorker(startupScript);
            super(server, worker.port);
            this.Worker = worker;
            this.Worker.port.start();
            var client = this;
            window.addEventListener("beforeunload", ev => {
                server.callClient({
                    id: client.Id,
                    method: "<disconnect>",
                });
            });
        }
    }

    export class WorkerClient<T> extends WorkerClientBase<T> {
        readonly Worker: Worker;
        readonly Id: number;

        constructor(server: IpcServer, startupScript: string) {
            var worker = new Worker(startupScript);
            super(server, worker);
            this.Worker = worker;
        }
    }

    export class IpcServer {
        public Target: any;
        private nextConnectionId = 1;
        private nextCallId = 1;
        private connections = new Map<number, IPort>();
        private pendingCalls = new Map<number, PromiseCompletionSource<any>>();

        constructor(target?: any) {
            this.Target = target;
            var server = this;

            if (WorkerGlobalScope.importScripts) {
                if (SharedWorkerGlobalScope.onconnect) {
                    SharedWorkerGlobalScope.onconnect = function (e) {
                        var port = e.ports[0];
                        server.connect(port);
                    }
                } else if (DedicatedWorkerGlobalScope.postMessage) {
                    this.connect(DedicatedWorkerGlobalScope);
                }
            }
        }

        public connect(port: IPort) {
            var id = this.nextConnectionId++;
            this.connections.set(id, port);;
            var server = this;

            port.onmessage = async function (ev) {
                var d: IMessage = ev.data;
                if (d.callId) {
                    let response: IPromiseResult = {};
                    try {
                        response.resolve = await server.Target[d.method](d.args);
                    } catch (ex) {
                        response.reject = ex;
                    }
                    port.postMessage({
                        returnCallId: d.callId,
                        args: response,
                        method: d.method,
                        id: d.id
                    });
                } else if (d.returnCallId) {
                    let response: IPromiseResult = d.args;
                    let pcs = server.pendingCalls.get(d.returnCallId);
                    if (response.reject) {
                        pcs.Reject(response.reject);
                    } else {
                        pcs.Resolve(response.resolve);
                    }
                } else if (d.method === "<disconnect>") {
                    server.disconnect(d.id);
                } else {
                    server.Target[d.method](d.args);
                }
            };

            return id;
        }

        public disconnect(id: number) {
            this.connections.delete(id);
        }

        public callClient(m: IMessage) {
            this.connections.get(m.id).postMessage(m);
        }

        public callClientAsync<T>(m: IMessage) : Promise<T> {
            m.callId = this.nextCallId++;
            var pcs = new PromiseCompletionSource<any>();
            this.pendingCalls.set(m.callId, pcs);

            this.connections.get(m.id).postMessage(m);
            return pcs.Task;
        }
    }
}