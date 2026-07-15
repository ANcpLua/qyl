import {renderToStaticMarkup} from 'react-dom/server';
import {describe, expect, it} from 'vitest';
import type {GenAiEtlClusterEvaluation} from '@/types';
import {
    buildScenarioRequest,
    EvaluationResultPanel,
    formatUsd,
    scenarioAssumptionKey,
} from './EtlAuditView';

describe('buildScenarioRequest', () => {
    it('sends an explicit frontier baseline when the user supplies one', () => {
        expect(buildScenarioRequest('workflow-1', {
            coverage: '0.8',
            frontier: '0.04',
            alternative: '0.01',
            maintenance: '12',
            error: '3',
        })).toEqual({
            request: {
                scenarios: [{
                    cluster_id: 'workflow-1',
                    coverage: 0.8,
                    frontier_cost_per_call_usd: 0.04,
                    alternative_cost_per_call_usd: 0.01,
                    period_maintenance_cost_usd: 12,
                    period_error_cost_usd: 3,
                }],
            },
        });
    });

    it('permits a blank frontier so the backend falls back to the catalog token estimate', () => {
        const built = buildScenarioRequest('workflow-1', {
            coverage: '0.8',
            frontier: '',
            alternative: '0.01',
            maintenance: '12',
            error: '3',
        });

        expect(built.error).toBeUndefined();
        expect(built.request?.scenarios[0]).not.toHaveProperty('frontier_cost_per_call_usd');
        expect(built.request?.scenarios[0]).toMatchObject({
            cluster_id: 'workflow-1',
            coverage: 0.8,
            alternative_cost_per_call_usd: 0.01,
            period_maintenance_cost_usd: 12,
            period_error_cost_usd: 3,
        });
    });

    it('rejects invalid coverage and negative cost inputs', () => {
        expect(buildScenarioRequest('workflow-1', {
            coverage: '1.1',
            frontier: '0.04',
            alternative: '0.01',
            maintenance: '12',
            error: '3',
        }).error).toContain('0 to 1');

        expect(buildScenarioRequest('workflow-1', {
            coverage: '0.5',
            frontier: '0.04',
            alternative: '-0.01',
            maintenance: '12',
            error: '3',
        }).error).toContain('non-negative');

        expect(buildScenarioRequest('workflow-1', {
            coverage: '0.5',
            frontier: '-0.04',
            alternative: '0.01',
            maintenance: '12',
            error: '3',
        }).error).toContain('Frontier cost per call must be a non-negative number.');
    });
});

describe('scenarioAssumptionKey', () => {
    const values = {
        coverage: '0.8',
        frontier: '0.04',
        alternative: '0.01',
        maintenance: '12',
        error: '3',
    };

    it('invalidates a displayed result when an assumption or its report scope changes', () => {
        const original = scenarioAssumptionKey(
            'workflow-1', 100, '2026-06-14T00:00:00Z', '2026-07-14T00:00:00Z', 'project-a', values,
        );

        expect(scenarioAssumptionKey(
            'workflow-1', 100, '2026-06-14T00:00:00Z', '2026-07-14T00:00:00Z', 'project-a',
            {...values, alternative: '0.02'},
        )).not.toBe(original);
        expect(scenarioAssumptionKey(
            'workflow-1', 100, '2026-06-14T00:00:00Z', '2026-07-14T00:00:00Z', 'project-b', values,
        )).not.toBe(original);
        expect(scenarioAssumptionKey(
            'workflow-1', 101, '2026-06-14T00:00:00Z', '2026-07-14T00:00:00Z', 'project-a', values,
        )).not.toBe(original);
        expect(scenarioAssumptionKey(
            'workflow-2', 100, '2026-06-14T00:00:00Z', '2026-07-14T00:00:00Z', 'project-a', values,
        )).not.toBe(original);
        expect(scenarioAssumptionKey(
            'workflow-1', 100, '2026-06-15T00:00:00Z', '2026-07-15T00:00:00Z', 'project-a', values,
        )).not.toBe(original);
    });
});

describe('EvaluationResultPanel', () => {
    it('renders missing frontier cost explicitly instead of zero', () => {
        const result: GenAiEtlClusterEvaluation = {
            cluster_id: 'workflow-1',
            status: 'missing_frontier_cost',
            call_count: 100,
            coverage: 0.5,
            served_call_count: 50,
            residual_call_count: 50,
            frontier_cost_basis: 'unavailable',
            alternative_cost_per_call_usd: 0.01,
            period_maintenance_cost_usd: 0,
            period_error_cost_usd: 0,
        };

        const html = renderToStaticMarkup(<EvaluationResultPanel result={result}/>);
        expect(html).toContain('Missing frontier cost');
        expect(html).toContain('not treated as zero');
    });

    it('retains and displays negative gross and net values', () => {
        const result: GenAiEtlClusterEvaluation = {
            cluster_id: 'workflow-1',
            status: 'calculated',
            call_count: 100,
            coverage: 0.5,
            served_call_count: 50,
            residual_call_count: 50,
            frontier_cost_per_call_usd: 0.01,
            frontier_cost_basis: 'scenario',
            alternative_cost_per_call_usd: 0.02,
            current_period_cost_usd: 1,
            gross_replaceable_value_usd: -0.5,
            period_maintenance_cost_usd: 2,
            period_error_cost_usd: 0.75,
            net_replaceable_value_usd: -3.25,
        };

        const html = renderToStaticMarkup(<EvaluationResultPanel result={result}/>);
        expect(html).toContain(formatUsd(-0.5));
        expect(html).toContain(formatUsd(-3.25));
        expect(html).toContain('Negative values are retained');
        expect(html).toContain('covered alternative spend exceeds the covered frontier spend');
        expect(html).not.toContain('attributable baseline');
    });
});
