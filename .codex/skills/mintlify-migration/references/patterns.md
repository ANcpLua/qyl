# Conversion Patterns Reference

## DocFX to Mintlify

### Callouts

**DocFX:**
```markdown
> [!NOTE]
> This is a note.

> [!WARNING]
> This is a warning.

> [!TIP]
> This is a tip.

> [!IMPORTANT]
> This is important.
```

**Mintlify:**
```mdx
<Note>
This is a note.
</Note>

<Warning>
This is a warning.
</Warning>

<Tip>
This is a tip.
</Tip>

<Warning>
This is important.
</Warning>
```

### Collapsible Sections

**DocFX/HTML:**
```html
<details>
<summary>Click to expand</summary>

Content here

</details>
```

**Mintlify:**
```mdx
<Accordion title="Click to expand">

Content here

</Accordion>
```

### Code with Title

**DocFX:**
```markdown
```csharp title="Example.cs"
// code
```
```

**Mintlify:**
```mdx
```csharp title="Example.cs"
// code
```
```

(Same syntax - no conversion needed)

### Tabs

**DocFX:**
```markdown
# [Tab 1](#tab/tab1)
Content 1

# [Tab 2](#tab/tab2)
Content 2
```

**Mintlify:**
```mdx
<Tabs>
  <Tab title="Tab 1">
    Content 1
  </Tab>
  <Tab title="Tab 2">
    Content 2
  </Tab>
</Tabs>
```

### Include Files

**DocFX:**
```markdown
[!INCLUDE [title](path/to/file.md)]
```

**Mintlify:**
```mdx
import Content from './path/to/file.mdx';

<Content />
```

### Cross-References

**DocFX:**
```markdown
<xref:System.String>
@System.String
```

**Mintlify:**
```mdx
[String](https://learn.microsoft.com/dotnet/api/system.string)
```

(Manual link - no auto-xref)

## MkDocs to Mintlify

### Admonitions

**MkDocs:**
```markdown
!!! note "Title"
    Content here

!!! warning
    Warning content
```

**Mintlify:**
```mdx
<Note title="Title">
Content here
</Note>

<Warning>
Warning content
</Warning>
```

### Tabs (Material theme)

**MkDocs:**
```markdown
=== "Python"
    ```python
    print("hello")
    ```

=== "JavaScript"
    ```javascript
    console.log("hello")
    ```
```

**Mintlify:**
```mdx
<CodeGroup>
```python Python
print("hello")
```

```javascript JavaScript
console.log("hello")
```
</CodeGroup>
```

## Link Format

**Source (any):**
```markdown
[Link](./other-page.md)
[Link](../section/page.md)
```

**Mintlify:**
```mdx
[Link](/section/other-page)
[Link](/section/page)
```

Rules:
- Remove `.md` / `.mdx` extensions
- Use absolute paths from root
- `index.md` becomes the directory path
