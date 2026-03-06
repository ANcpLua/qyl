People often ask me, "If I could do any
video and not worry about the algorithm,
what would that video be?" To be honest,
most of the videos I want to do, I want
to do some amount because I like the
topic, but also some amount because of
the reach. But this is one I kind of
expect to bomb. Why am I doing it then?
Because I really care about this topic.
This is a huge change and it touches on
a ton of the nerdy weird details that I
love to dig into and hopefully it will
perform well. OpenAI just made a huge
change to their APIs. They are no longer
just doing everything through REST calls
like you know everyone is and they're
moving over to websockets. This is a
massive shift even though it might not
seem that way. We're talking about
reducing bandwidth by 90 plus% and speed
improvements by 20 to 30%. Which sounds
crazy just going from SSE to websocket,
right? Well, yeah. If you don't know how
AI models work, believe it or not, this
video is going to be a deep dive both
into networking technology and into the
weird characteristics of how AI models
and agents in particular actually do the
work they do. If you don't yet
understand the relationship between the
thing you're running in your terminal or
your editor and the actual servers that
are generating the outputs that go
there, this is going to be a very useful
video. And if you do already understand
that, this will probably be even more
useful and will be a good resource for
you to send to your co-workers who
almost certainly don't get any of this.
I am regularly surprised by just how
many developers don't understand the way
context works and how requests work back
and forth and any of that at all. And I
hope this will help you guys understand
it a lot better. But there's one other
thing I need you all to understand
first. How great today's sponsor is. How
many times have you had a change you
were ready to ship, but you were blocked
waiting for CI to build just for it to
finish and give you a random error you
had to go fix and then you're waiting
again for sometimes hours just to get
small changes out. That's because the CI
tools we're using suck. Unless you're
using today's sponsor, Blacksmith, they
make your CI comically faster. Even for
my simple TypeScript code bases, build
times are often cut in half from over 3
minutes to under a minute for so many
projects I work on. And if you happen to
be using Docker, the 40 times faster
Docker builds are a godsend. All of your
Docker layers are cached on NVMe drives
on the same computer. So you end up with
way faster caching, downloading, and the
whole build process with Docker. They
have really good examples on the site,
including the Android GitHub actions,
which used to take 9 minutes to run on
official GitHub CI, but with a oneline
change, it's now 3 minutes, and it costs
a fourth as much. We've been using them
for all of our CI at ping, and the team
is hooked. Whenever a new repo appears
and we start adding CI, we realize we
forgot to add Blacksmith. We make the
one line of code change and suddenly our
builds take under a minute again. It's
great. And you'll see here, even for
some of our bigger projects, our builds
take consistently 50ish seconds. I would
go in depth on how good the
observability is, but I'll just sit here
and talk for hours. It's so much better
than anything GitHub has published in
decades. It's insane. If you actually
want to be able to debug your actions,
you need to move to Blacksmith. If you
want to pay half as much for your
actions, you should use Blacksmith. If
you want your CI to be way faster, you
should use Blacksmith. If you want your
Docker bills to be way faster, you
should use Blacksmith. Generally
speaking, you should probably check them
out at soy.link/blacksmith.
So, why am I so excited about this
websocket move? The first thing I want
to make clear is that we're not yet
using this for T3 chat. And honestly,
for the T3 chat use case, it doesn't
make a ton of sense. But there are other
places where this makes a absurd amount
of sense. First, I want to make sure we
understand how context management and
requests work with an AI model. So, we
have the system prompt up top here. We
then have the user message. Let's do a
traditional one like, I want to improve
the SEO of my application. Let's write a
thorough plan on how we're going to make
this app have better SEO. Classic
standard user message. And we'll have, I
don't know, red be the response from the
agent. And the agent says, "Okay, first
let's build a better understanding
of this codebase." So user makes a
prompt. Agent decides it needs to go
understand the codebase better. So what
does it do? It calls tools. I don't know
uh lsa or something where it's going to
list all of the stuff in a given
directory. What does that tool call?
Then it says interesting. The important
code is probably in source slash. So,
let's scan that next. You get the idea.
And after a bunch of those happen, we
end up with the final answer. Your SEO
has been improved. I edited D. You get
the idea. We've all built with agents.
We've all seen this. This is your
context in that moment. And obviously,
as things go, the context builds. You
have the system prompt at the top, then
the user message, then an agent message.
The agent message ends with a tool call.
The tool call fires, gets results, the
agent then responds, makes the next set
of tool calls, and then eventually you
get the actual result. You get the idea.
So, what happens when you want to do a
follow-up message? Like, I don't know,
let's say at the end here, you say that
looks good, but I want to change the
description or something like that. What
request gets sent to the OpenAI API when
you send this follow-up message? What is
contained within that? You've been
around for a bit, you know. And if we
look at our chat, you see it. All of it.
Everything. Everything. Yep, you guys
got it. Follow-up request isn't just you
sending your new message. It's you
sending the whole thing. But that's not
even where the pain point is. If that
was all, it wouldn't be too big a deal.
Sending all of this text once per user
message would be fine. What if I told
you it was sent significantly more than
that, though? What if I told you it got
sent here when you sent the user
message, here when the first tool call
completed, here when the second tool
call completed, and then again when you
sent the new prompt. Every single tool
call sends the whole history back. And
it makes sense if you think about it
because here we have the agent call.
Okay, first let's build a better
understanding of the codebase. and it
outputs a tool call. The agent is just
autocomplete. It's just generating
words. It doesn't know what's happening.
It doesn't know what system it is. It
doesn't know how long it will take.
People seem to think that when the tool
call happens, a pause occurs, but that
is not what's happening. A tool call
means the generation finished. The AI
isn't running when the tool call
happens. The AI is paused effectively.
It's dead. It's killed. It's off. And
when the tool call is done, everything
from here up gets sent back to the AI,
sent back over the API call to whatever
service is running it. And now it has a
new history that it is using to continue
generation from. Somebody asked, is this
to verify the tool call worked? No, it's
to have the information from the tool
call. The tool call was list stuff. It
was an ls call. So now there's an
output, but it can't do anything until
it has that output. So it just stops. So
every time a tool call happens, the
model needs to know what the tool call
result is. So once the tool call is
done, it then sends everything back
because it needs everything. It's almost
like a Groundhog Day type thing where
every time the model wakes up, there's
nothing left in its brain. It's
stateless. It has nothing going on. So
all of the state has to be shipped back
every single time. So if you like watch
your network traffic when you're doing a
long agent run, every one of these tool
calls is sending the whole history back
up to the anthropic API to the OpenAI
API to whatever else. And then whatever
GPU is available takes in all of that
context and then loads it for the next
response. And I already know what you
guys are thinking. Well, isn't that what
caching's for? The cache is just to
reduce the compute the model has to do,
which makes it way cheaper and also
faster to get the first token, but it's
not changing how much data you send
because the key for that cache is a hash
of your history. So if this part's all
been cached, you're not telling the AI,
hey, go pull from that cache and then
add this to it. You're passing the whole
history and then it hashes it and says,
oh we have the history up to this
point cached. The new part is here. So,
we're going to load the state of things
up until that new thing. Then, we're
going to add the new thing. Then, we're
going to start responding. That's what
the cache does. The cache does not
change how much data you send. The cache
just changes how long it takes to
process the data. It's effectively a
hash for that. So, the cache is just a
compute reduction method. Is not
changing what traffic has to be sent at
all. Want to make sure that's very, very
clear. Someone asked if this is what
compaction is. And no, compaction is
very different. Compaction couldn't be
more different. Compaction is taking the
entire history and summarizing it so you
don't need all of the tokens anymore.
But compaction breaks the cache. It does
not help the cache. Compaction is
choosing to forgo the cache because you
want to have a shorter history. Anyways,
hopefully you can somewhat see the
problem. Now, the problem isn't that
it's too hard for the model to get this
data. It's very simply that sending all
of this up every single time a tool call
happens. A growing amount of data every
single time just kind of sucks. It's
really inefficient. It's really
expensive. It uses a shitload of
bandwidth. It means running a ton of
agents in parallel is more expensive
than it needs to be. It means OpenAI's
APIs need to be able to handle these
giant text payloads and the routing and
all of those things. Wouldn't it be much
easier if instead of sending all of
this, it only had to send the new thing,
the tool call. Wouldn't that be cool?
But that makes things more complex
because effectively OpenAI's
infrastructure doesn't really allow
that. So OpenAI has a bunch of GPUs, but
you're not accessing the GPUs. You're
not sending this request straight to a
GPU, obviously, because then we have to
format everything entirely differently.
It make no sense. And also that GPU
might not have your specific cache on
it. So effectively all of these requests
have to go through an orchestration
layer, an API if you will. So in front
of OpenAI's GPUs, they have this API.
And of course, they don't have one GPU,
they have thousands of them, tens of
thousands of them across lots of
different places. So your request goes
to the API. Once your request goes to
the API, it checks things. It checks
like, do we have a cached value for
where we were before here? Do we have a
GPU open that we can route this to? Does
this user have permission to do this
thing? And then it routes whatever it
concludes here over to a GPU. And if
that goes well, it responds with those
tokens. And those tokens then get sent
back to you, which ends up being the
agent response. But also remember the
API is not as simple as it may seem
here. This is also lots of separate API
boxes that all look for random available
GPUs and route to those. So your first
request might go to the first API box.
Your second request might go to the
fourth API box and then this one might
go to a different GPU entirely. You see
the problem here? Even if the servers
were to maintain this state, making sure
the right one has the right context is
obnoxious. And I'm pretty sure the
caching layer is a whole separate third
layer thing that has to manage all of
this. So when you send the request, it
goes to a server that OpenAI runs that
is a relatively dumb server that they
have tons of that has to go check which
cache might have the cached value for
your tokens and then load that state
into a GPU, load your prompt into the
GPU, generate tokens, and then respond.
And get this, it has to do all of that
for generations that are sometimes only
10 tokens. Since this API layer doesn't
have your history, doesn't know what you
sent before because every additional
request gets routed somewhere else, you
have to resend the whole thing because
otherwise it loses it. They could try to
like attach an ID the whole time and
keep everything cached always, but they
would never know how long they have to
keep it around. The latency problems
there of making that globally scaled
across it, like not viable, not viable
at all. The architecture for that would
have to be an external data store that
all the APIs connect to that every
single request has to be cached in. Not
viable. Not viable at all. Not for that
amount of data. And not for things where
half the time it doesn't even matter.
And remember, sometimes these responses
are going to be really short like maybe
it's wrong directory trying.
This is a real thing. The agent might
respond. The agent might ingest a
100,000 tokens that you sent over API.
It might literally be sent two megabytes
of text and then respond with eight
words. That's a real thing that happens
to us every single day. You are
processing so much data for just a few
tokens to be generated. That's so
wasteful. And there's basically nothing
you can do at this layer that keeps it
scaled as well as it is that also
persists the state externally. So it
doesn't matter which you hit throughout.
There is a solution though. What if you
could guarantee that every single
request hits the same box? What if you
could be sure that tool call 2 goes the
same box that tool call one did? What if
you could be sure that all of these
follow-ups, all of these things
happening during this session are
hitting the same box? Then you wouldn't
have to load all of this from storage.
You wouldn't have to check what's cached
you already know. You wouldn't even have
to send up all of the context. You could
just send the new message or just the
new tool call. You wouldn't have to send
much of anything. But it would have to
be stateful. The way that OpenAI's API
worked previously was effectively
stateless. These boxes didn't know
anything. They had to go look up data.
They had to go check the database to see
if you had a cache entity or not. They
had to go check the O server to make
sure you were allowed to do the thing.
And every single tool call had to
doublech checkck every single thing.
Every request had to check everything.
But if the first request had everything
covered, does the second request really
need all of that? Like what if it just
knew after the first request who you
were, what you were doing, what cache
you could hit. This is why we're talking
about websockets today. The value of
websockets here isn't that it's a better
protocol. It's not that it is magically
faster cuz websockets are this gift from
the heavens. It is very simple.
Websockets can guarantee that you're
hitting the same box. Since you're
maintaining the connection to that API
server through the whole generation, you
don't have to worry about it losing its
state. You don't have to worry about
rechecking off. You don't have to worry
about knowing what's cached or not. The
websocket is less a protocol here, more
a guarantee. The guarantee is that as
this generation is running, you can keep
hitting the same server box that is now
capable of keeping track of what you've
done so far. You don't have to recheck
your O. You don't have to resend the
whole context. You just send what's new
and then it can immediately connect to a
GPU, feed everything over there, and
start responding. This reduces how much
data you send. This reduces how much
data has to be processed. This reduces
how many checks have to be done, how
much cache has to be touched, how many
things have to occur from when the tool
call completes on your machine to when
the API is aware of what's going on.
That is awesome. And it is insane it
took this long for something like this
to happen. And I am very, very, very
thankful OpenAI are the ones to do it
because OpenAI's implementation for
these things tends to become the
standard. Not only are OpenAI standards
cloned by pretty much everyone, they
actually fully open sourced it. Open
responses is a standard that is inspired
by the OpenAI responses API. Binds a
shared request response model, streaming
semantics, and tool invocation pattern.
So clients and providers can exchange
structured inputs and outputs in a
consistent shape. You can use this shape
for calling the APIs for everything from
anthropic to Gemini. And now it's an
open standard. And sadly, no, the
standard does not have the websocket
piece implemented yet. I would expect
this to be implemented very, very soon.
Yeah, I'm very thankful who is involved
with all of this. This being implemented
the way it was, the place it was,
massively increases the chance that this
does not just become standard number 72
and instead becomes a new default that
these agentic tools can use. And to be
very very clear, just to be
extraordinarily clear, the benefit here
is not huge in like the chat app use
case because persisting that connection
is not worth it if you're not following
up immediately. So the second user
message here is probably not going to go
over the same websocket and it's
probably not going to hit the same API
box. And that's fine. Reloading all of
that context once per user message is
totally reasonable. Doing it once per
tool call where sometimes one user
message could spawn hundreds of tool
calls that is much less okay. And if
you're curious about the results, this
is what OpenAI had to say. And as
somebody who was lucky enough to test
this early, I can confirm it might even
be crazier than what they're sharing
here. Websockets keep a persistent
connection to the responses API,
allowing you to send only new inputs
instead of sending round trips for the
entire context on every turn. By
maintaining inmemory state across
interactions, it avoids repeated work
and speeds up agentic runs with 20 plus
tool calls by 20 to 40%. And you can see
what this looks like in their little
demo video.
Do you understand how massive this is?
There are so many things that matter in
this space that aren't just how quick
can the model generate tokens. When we
say we're early to an extent, this is
the stuff we're talking about. On one
hand, this is so cool. Like, yeah, we
now have a way to keep things in state
and massively improve the performance.
On the other hand, how crazy early are
we that we were just okay with sending
the whole context every single time this
type of thing was generated because we
just stapled on tool calling to existing
APIs and standards. Like there's so much
opportunity here. I hope this gets you
excited to build. There's so much space
to make useful improvements across the
entire stack. So many things we just do
a certain way because that's how it
started. So many tools we still rely on
because they're just what we're used to.
Everything in our stack from how we
network to how we send requests to how
we store our code in Git is all up for
change right now. This is really fun. To
those thinking that deep tech planning
and thought and process are dead as a
result of AI, you're wrong. It matters
more than ever. We get to rethink
everything from first principles right
now. And it's fun. It's genuinely fun. I
was so hyped when OpenAI told me about
the changes they were making here. I was
really lucky to get to play with it a
bit early. And I'm so thankful that I
get to share this all with you guys now
because as nerdy as this is and as much
as I expect this video to bomb, I really
hope it does well because this stuff is
so much cooler than just whatever the
new model is. I am genuinely more
excited about this than Codex Spark than
about Sonnet 46 than about basically any
of the openweight models that have come
out recently. I'm personally way more
hyped about the small API change. Yeah.
Thank you all for nerding out with me. I
hope you enjoy this as much as I do.
Until next time, peace nerds.


## A Deep Dive into Websockets, State, and the OpenAI API Shift

Intro
- This descriptive summary explore a detailed technical fever dream: how a shift from REST to WebSockets could redefine how AI agents manage context, cache results, and talk to GPUs across vast, distributed infrastructures.
- The author blends networking nerdcraft with practical AI behavior to argue that the real win is maintaining state across long agent runs, not just faster token generation.

Center
<div align="center">
  <em>Key concepts and takeaways</em>
  <ul>
    <li><strong>Context and prompts:</strong> The system prompt, user input, and successive tool calls compose a growing, tool-driven dialogue. Every tool invocation returns results that must be reintegrated into the conversation history.</li>
    <li><strong>Stateless vs. stateful:</strong> Traditional APIs reset context between requests, forcing replays of the entire history. A truly stateful connection preserves context, dramatically reducing redundant data transfers.</li>
    <li><strong>Tool calls and history:</strong> Each tool call emits a full history back to the AI for continuation. This heavy, repeated payload is both expensive and bandwidth-intensive.</li>
    <li><strong>Caching and compaction:</strong> Caching reduces compute by reusing prior results but does not reduce the data sent. Compaction summarizes history, yet it undermines cache effectiveness by shortening the history.</li>
    <li><strong>Architectural bottlenecks:</strong> Routing through a large fleet of API boxes and GPUs forces every request to retread checks (permissions, cache hits) and rehydrate state, which inflates latency and bandwidth.</li>
    <li><strong>WebSockets as a guarantee, not a miracle:</strong> The primary value is maintaining a persistent session with a single API box, allowing continued stateful interaction and minimized data transfer per turn.</li>
    <li><strong>Open standards:</strong> OpenAI’s responses API and the emerging Open responses standard promote consistent inputs/outputs; websocket integration is anticipated to mature into a shared baseline.</li>
  </ul>
</div>

Table: comparing workflows
| Aspect | REST (stateless) | WebSocket (stateful) |
|---|---|---|
| Context handling | Re-sent per turn | Maintains session context |
| Tool calls | Full history re-sent each time | Only new inputs; reuse of prior state |
| Data transfer | High, repeated | Lower, incremental |
| Caching impact | Partial gains | Greater efficiency via persistence |
| Latency | Higher due to repeats | Lower with persistent route |
| Complexity | Simpler backend, heavier client work | More complex orchestration, but faster runs |

- The video’s longer argument emphasizes how the logistics of OpenAI’s API orchestration layers—where requests travel through multiple boxes and GPUs—create nontrivial inefficiencies when every turn revalidates the entire history.
- In practice, websockets could ensure that subsequent tool calls and follow-ups hit the same backend box, avoiding repeated state reconciliation, and enabling immediate GPU feeding and token generation.
- The author highlights that this is not merely about speed; it is about reshaping the entire stack—from networking to how we store and retrieve code in Git—toward a more principled, stateful model of conversation with AI agents.
- OpenAI’s approach, openness to standardization, and potential widespread adoption could set a durable baseline for agentic tools across vendors, including anthropic and Gemini.

Outro
- The takeaway is exciting: a seemingly modest API change unlocks outsized gains in efficiency for long-running agent workflows.
- The writer believes we are just beginning to rethink foundational assumptions and that this shift will catalyze broad, creative improvements across the entire AI stack.
- _Thank you for nerding out with me_; the future of stateful AI conversations is bright, practical, and ready for rapid iteration.
