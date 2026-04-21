/**
 * Ring Buffer for O(1) append with automatic pruning.
 * Used for high-volume log streaming where array spread would cause GC pressure.
 *
 * Features:
 * - O(1) push (overwrites oldest when full)
 * - O(1) get by index
 * - Generation tracking for cache invalidation on wrap-around
 * - Batch push support
 */
export class RingBuffer<T> {
    private buffer: (T | undefined)[];
    private head = 0; // Next write position
    private tail = 0; // Oldest item position

    constructor(private readonly capacity: number) {
        if (capacity <= 0) {
            throw new Error('RingBuffer capacity must be positive');
        }
        this.buffer = new Array(capacity);
    }

    private _length = 0;

    /** Current number of items in buffer */
    get length(): number {
        return this._length;
    }

    private _generation = 0; // Increments on wrap-around (indices shifted - invalidate caches)

    /**
     * Generation counter - increments each time buffer wraps around.
     * Use this to invalidate cached measurements in virtualizers (indices shifted).
     */
    get generation(): number {
        return this._generation;
    }

    private _version = 0; // Increments on any push (content changed - trigger re-render)

    /**
     * Version counter - increments on every push.
     * Use this as a React dependency to trigger re-renders on content changes.
     */
    get version(): number {
        return this._version;
    }

    /** Maximum capacity */
    get size(): number {
        return this.capacity;
    }

    /** Whether the buffer has wrapped at least once */
    get hasWrapped(): boolean {
        return this._generation > 0;
    }

    /**
     * Add a single item. O(1) operation.
     * If buffer is full, overwrites the oldest item.
     */
    push(item: T): void {
        this.buffer[this.head] = item;
        this.head = (this.head + 1) % this.capacity;
        this._version++; // Content changed

        if (this._length < this.capacity) {
            this._length++;
        } else {
            // Wrapped around - oldest item was overwritten
            this.tail = (this.tail + 1) % this.capacity;
            this._generation++; // Indices shifted
        }
    }

    /**
     * Add multiple items. More efficient than multiple push() calls
     * when batching SSE messages.
     */
    pushMany(items: readonly T[]): void {
        for (const item of items) {
            this.push(item);
        }
    }

    /**
     * Get item by logical index (0 = oldest, length-1 = newest).
     * O(1) operation.
     */
    get(index: number): T | undefined {
        if (index < 0 || index >= this._length) {
            return undefined;
        }
        const actualIndex = (this.tail + index) % this.capacity;
        return this.buffer[actualIndex];
    }

    /**
     * Get the newest item (most recently pushed).
     */
    newest(): T | undefined {
        if (this._length === 0) return undefined;
        const index = (this.head - 1 + this.capacity) % this.capacity;
        return this.buffer[index];
    }

    /**
     * Get the oldest item (will be overwritten next on full buffer).
     */
    oldest(): T | undefined {
        if (this._length === 0) return undefined;
        return this.buffer[this.tail];
    }

    /**
     * Convert to array (oldest first).
     * O(n) - use sparingly, mainly for filtering.
     */
    toArray(): T[] {
        const result: T[] = new Array(this._length);
        for (let i = 0; i < this._length; i++) {
            result[i] = this.get(i)!;
        }
        return result;
    }

    /**
     * Convert to array (newest first).
     * Useful for displaying logs in reverse chronological order.
     */
    toArrayReversed(): T[] {
        const result: T[] = new Array(this._length);
        for (let i = 0; i < this._length; i++) {
            result[i] = this.get(this._length - 1 - i)!;
        }
        return result;
    }

    /**
     * Iterate over items (oldest to newest).
     */
    * [Symbol.iterator](): Iterator<T> {
        for (let i = 0; i < this._length; i++) {
            yield this.get(i)!;
        }
    }

    /**
     * Iterate over items without allocation.
     * Preferred over toArray() when you need to process items.
     */
    forEach(callback: (item: T, index: number) => void): void {
        for (let i = 0; i < this._length; i++) {
            callback(this.get(i)!, i);
        }
    }

    /**
     * Filter items without creating intermediate array.
     * Returns new array with matching items.
     */
    filter(predicate: (item: T, index: number) => boolean): T[] {
        const result: T[] = [];
        for (let i = 0; i < this._length; i++) {
            const item = this.get(i)!;
            if (predicate(item, i)) {
                result.push(item);
            }
        }
        return result;
    }

    /**
     * Find first item matching predicate (searches oldest to newest).
     */
    find(predicate: (item: T) => boolean): T | undefined {
        for (let i = 0; i < this._length; i++) {
            const item = this.get(i)!;
            if (predicate(item)) {
                return item;
            }
        }
        return undefined;
    }

    /**
     * Find last item matching predicate (searches newest to oldest).
     * More efficient for finding recent items.
     */
    findLast(predicate: (item: T) => boolean): T | undefined {
        for (let i = this._length - 1; i >= 0; i--) {
            const item = this.get(i)!;
            if (predicate(item)) {
                return item;
            }
        }
        return undefined;
    }

    /**
     * Clear all items. Increments both version and generation.
     */
    clear(): void {
        // Help GC by clearing references before reassigning
        this.buffer.fill(undefined);
        this.buffer = new Array(this.capacity);
        this.head = 0;
        this.tail = 0;
        this._length = 0;
        this._version++;
        this._generation++;
    }
}
