/**
 * User interaction (click/input) span capture.
 * Records click and input events with element selectors for replay.
 */

import type {OtlpSpan} from './types.js';
import {dateNowNano, generateSpanId, generateTraceId} from './context.js';
import type {Transport} from './transport.js';

/** Build a concise CSS selector for an element. */
function getSelector(el: Element): string {
    if (el.id) return `#${el.id}`;
    const tag = el.tagName.toLowerCase();
    const classes = el.className && typeof el.className === 'string'
        ? `.${el.className.trim().split(/\s+/).slice(0, 3).join('.')}`
        : '';
    const text = el.textContent?.trim().slice(0, 30);
    const textHint = text ? ` "${text}"` : '';
    return `${tag}${classes}${textHint}`;
}

function interactionToSpan(event: Event): OtlpSpan {
    const target = event.target as Element | null;
    const now = dateNowNano();
    return {
        traceId: generateTraceId(),
        spanId: generateSpanId(),
        name: `interaction.${event.type}`,
        kind: 1, // INTERNAL
        startTimeUnixNano: now,
        endTimeUnixNano: now,
        attributes: [
            {key: 'interaction.type', value: {stringValue: event.type}},
            {key: 'interaction.target', value: {stringValue: target ? getSelector(target) : 'unknown'}},
            {key: 'interaction.tag', value: {stringValue: target?.tagName.toLowerCase() ?? 'unknown'}},
            {key: 'browser.url', value: {stringValue: location.href}},
        ],
    };
}

let clickHandler: ((e: Event) => void) | null = null;

export function startInteractionCapture(transport: Transport): void {
    clickHandler = (event: Event) => {
        transport.addSpan(interactionToSpan(event));
    };

    // Use capture phase to get events even if stopped
    document.addEventListener('click', clickHandler, {capture: true});
}

export function stopInteractionCapture(): void {
    if (clickHandler) {
        document.removeEventListener('click', clickHandler, {capture: true});
        clickHandler = null;
    }
}
