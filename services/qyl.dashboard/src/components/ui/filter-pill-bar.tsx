import {useRef, useState} from 'react';
import {X} from 'lucide-react';
import {cn} from '@/lib/utils';

export interface FilterPill {
    id: string;
    field: string;
    value: string;
}

interface FilterPillBarProps {
    pills: FilterPill[];
    onChange: (pills: FilterPill[]) => void;
    availableFields: string[];
    placeholder?: string;
    className?: string;
}

export function FilterPillBar({
                                  pills,
                                  onChange,
                                  availableFields,
                                  placeholder = 'Filter...',
                                  className
                              }: FilterPillBarProps) {
    const [input, setInput] = useState('');
    const [showFields, setShowFields] = useState(false);
    const inputRef = useRef<HTMLInputElement>(null);

    const unusedFields = availableFields.filter(
        (f) => f.toLowerCase().includes(input.toLowerCase()) && !pills.some((p) => p.field === f),
    );

    return (
        <div className={cn('relative', className)}>
            <div
                className="flex flex-wrap items-center gap-1.5 min-h-9 px-3 py-1.5 bg-brutal-carbon border-2 border-brutal-zinc focus-within:border-primary/50"
                onClick={() => inputRef.current?.focus()}
            >
                {pills.map((pill) => (
                    <span key={pill.id}
                          className="inline-flex items-center gap-1 px-2 py-0.5 bg-brutal-dark border border-brutal-zinc text-xs font-mono">
                        <span className="text-brutal-slate">{pill.field}</span>
                        <span className="text-brutal-zinc">is</span>
                        <span className="text-brutal-white">{pill.value || '…'}</span>
                        <button
                            onClick={(e) => {
                                e.stopPropagation();
                                onChange(pills.filter((p) => p.id !== pill.id));
                            }}
                            className="ml-0.5 p-0.5 hover:bg-brutal-zinc/50 text-brutal-slate hover:text-brutal-white"
                            aria-label={`Remove ${pill.field} filter`}
                        >
                            <X className="w-3 h-3"/>
                        </button>
                    </span>
                ))}
                <input
                    ref={inputRef}
                    value={input}
                    onChange={(e) => {
                        setInput(e.target.value);
                        setShowFields(true);
                    }}
                    onFocus={() => setShowFields(true)}
                    onBlur={() => setTimeout(() => setShowFields(false), 150)}
                    onKeyDown={(e) => {
                        if (e.key === 'Backspace' && input === '' && pills.length > 0)
                            onChange(pills.slice(0, -1));
                        if (e.key === 'Escape') setShowFields(false);
                    }}
                    placeholder={pills.length === 0 ? placeholder : 'Add filter…'}
                    className="flex-1 min-w-[120px] bg-transparent border-none outline-hidden focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-signal-orange text-sm text-brutal-white placeholder:text-brutal-slate"
                    aria-label="Add filter"
                />
            </div>

            {showFields && unusedFields.length > 0 && (
                <div
                    className="absolute z-50 top-full left-0 right-0 mt-1 bg-brutal-carbon border-2 border-brutal-zinc max-h-48 overflow-auto">
                    {unusedFields.map((field) => (
                        <button
                            key={field}
                            className="w-full px-3 py-2 text-left text-sm text-brutal-slate hover:bg-brutal-dark hover:text-brutal-white font-mono"
                            onMouseDown={(e) => {
                                e.preventDefault();
                                onChange([...pills, {id: `${field}-${Date.now()}`, field, value: ''}]);
                                setInput('');
                                setShowFields(false);
                            }}
                        >
                            {field}
                        </button>
                    ))}
                </div>
            )}
        </div>
    );
}
