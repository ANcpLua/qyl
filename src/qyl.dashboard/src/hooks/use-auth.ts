import {useMutation, useQuery, useQueryClient} from '@tanstack/react-query';
import {useNavigate} from 'react-router-dom';
import {toast} from 'sonner';

interface LoginResponse {
    success: boolean;
    error?: string;
}

interface AuthStatus {
    authenticated: boolean;
}

export const authKeys = {
    status: ['auth', 'status'] as const,
};

async function checkAuth(): Promise<AuthStatus> {
    const res = await fetch('/api/auth/check', {credentials: 'include'});
    if (!res.ok) throw new Error('Auth check failed');
    return res.json();
}

async function login(token: string): Promise<LoginResponse> {
    const res = await fetch('/api/login', {
        method: 'POST',
        headers: {'Content-Type': 'application/json'},
        credentials: 'include',
        body: JSON.stringify({token}),
    });
    return res.json();
}

async function logout(): Promise<void> {
    await fetch('/api/logout', {
        method: 'POST',
        credentials: 'include',
    });
}

export function useAuthStatus() {
    return useQuery({
        queryKey: authKeys.status,
        queryFn: checkAuth,
        retry: false,
        staleTime: 1000 * 60,
    });
}

export function useLogin() {
    const queryClient = useQueryClient();
    const navigate = useNavigate();

    return useMutation({
        mutationFn: login,
        onSuccess: (data) => {
            if (data.success) {
                queryClient.invalidateQueries({queryKey: authKeys.status});
                toast.success('Logged in successfully');
                navigate('/');
            } else {
                toast.error(data.error || 'Invalid token');
            }
        },
        onError: () => {
            toast.error('Login failed');
        },
    });
}

export function useLogout() {
    const queryClient = useQueryClient();
    const navigate = useNavigate();

    return useMutation({
        mutationFn: logout,
        onSuccess: () => {
            queryClient.invalidateQueries({queryKey: authKeys.status});
            toast.success('Logged out');
            navigate('/login');
        },
    });
}
