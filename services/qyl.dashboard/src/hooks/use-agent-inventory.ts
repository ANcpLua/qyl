import {useQuery} from '@tanstack/react-query';

export interface AgentRegistration {
    key: string;
    name: string;
    description: string | null;
    instructionsHash: string | null;
    providerName: string | null;
    registeredAtUtc: string;
}

export interface AgentInventoryResponse {
    items: AgentRegistration[];
    total: number;
}

export interface AgentInventoryState {
    items: AgentRegistration[];
    total: number;
    /**
     * Inventory may be unavailable in production when no auth is wired
     * (the endpoint returns 404 instead of leaking agent keys + instructions
     * hashes anonymously). The UI degrades to an empty + explanatory state
     * rather than crashing.
     */
    available: boolean;
    statusCode: number | null;
}

export const agentInventoryKeys = {
    all: ['agent-inventory'] as const,
};

export function useAgentInventory() {
    return useQuery<AgentInventoryState>({
        queryKey: agentInventoryKeys.all,
        queryFn: async () => {
            const res = await fetch('/qyl/inventory/agents', {
                headers: {'Accept': 'application/json'},
            });

            if (res.status === 401 || res.status === 403 || res.status === 404) {
                return {items: [], total: 0, available: false, statusCode: res.status};
            }

            if (!res.ok) {
                throw new Error(`Inventory fetch failed: ${res.status} ${res.statusText}`);
            }

            const body: AgentInventoryResponse = await res.json();
            return {
                items: body.items ?? [],
                total: body.total ?? 0,
                available: true,
                statusCode: res.status,
            };
        },
        staleTime: 30_000,
        retry: false,
    });
}
