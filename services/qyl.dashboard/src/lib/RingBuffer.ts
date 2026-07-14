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
    private head = 0;
    private tail = 0;

    constructor(private readonly capacity: number) {
        if (capacity <= 0) {
            throw new Error('RingBuffer capacity must be positive');
        }
        this.buffer = new Array(capacity);
    }

    private _length = 0;

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

    get size(): number {
        return this.capacity;
    }

    get hasWrapped(): boolean {
        return this._generation > 0;
    }

    push(item: T): void {
        this.buffer[this.head] = item;
        this.head = (this.head + 1) % this.capacity;
        this._version++;

        if (this._length < this.capacity) {
            this._length++;
        } else {
            this.tail = (this.tail + 1) % this.capacity;
            this._generation++;
        }
    }

    pushMany(items: readonly T[]): void {
        for (const item of items) {
            this.push(item);
        }
    }

    get(index: number): T | undefined {
        if (index < 0 || index >= this._length) {
            return undefined;
        }
        const actualIndex = (this.tail + index) % this.capacity;
        return this.buffer[actualIndex];
    }

    newest(): T | undefined {
        if (this._length === 0) return undefined;
        const index = (this.head - 1 + this.capacity) % this.capacity;
        return this.buffer[index];
    }

    oldest(): T | undefined {
        if (this._length === 0) return undefined;
        return this.buffer[this.tail];
    }

    toArray(): T[] {
        const result: T[] = new Array(this._length);
        for (let i = 0; i < this._length; i++) {
            result[i] = this.get(i)!;
        }
        return result;
    }

    toArrayReversed(): T[] {
        const result: T[] = new Array(this._length);
        for (let i = 0; i < this._length; i++) {
            result[i] = this.get(this._length - 1 - i)!;
        }
        return result;
    }

    * [Symbol.iterator](): Iterator<T> {
        for (let i = 0; i < this._length; i++) {
            yield this.get(i)!;
        }
    }

    forEach(callback: (item: T, index: number) => void): void {
        for (let i = 0; i < this._length; i++) {
            callback(this.get(i)!, i);
        }
    }

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

    find(predicate: (item: T) => boolean): T | undefined {
        for (let i = 0; i < this._length; i++) {
            const item = this.get(i)!;
            if (predicate(item)) {
                return item;
            }
        }
        return undefined;
    }

    findLast(predicate: (item: T) => boolean): T | undefined {
        for (let i = this._length - 1; i >= 0; i--) {
            const item = this.get(i)!;
            if (predicate(item)) {
                return item;
            }
        }
        return undefined;
    }

    clear(): void {
        this.buffer.fill(undefined);
        this.buffer = new Array(this.capacity);
        this.head = 0;
        this.tail = 0;
        this._length = 0;
        this._version++;
        this._generation++;
    }
}
