# tactiq.io free youtube transcript
# I can't believe nobody's done this before...
# https://www.youtube.com/watch/sFEDAkJy9Dc

00:00:00.080 People often ask me, &quot;If I could do any
00:00:02.000 video and not worry about the algorithm,
00:00:04.319 what would that video be?&quot; To be honest,
00:00:06.400 most of the videos I want to do, I want
00:00:08.080 to do some amount because I like the
00:00:09.519 topic, but also some amount because of
00:00:11.040 the reach. But this is one I kind of
00:00:12.880 expect to bomb. Why am I doing it then?
00:00:15.759 Because I really care about this topic.
00:00:17.440 This is a huge change and it touches on
00:00:18.960 a ton of the nerdy weird details that I
00:00:21.600 love to dig into and hopefully it will
00:00:23.920 perform well. OpenAI just made a huge
00:00:26.240 change to their APIs. They are no longer
00:00:28.080 just doing everything through REST calls
00:00:29.920 like you know everyone is and they're
00:00:31.760 moving over to websockets. This is a
00:00:33.760 massive shift even though it might not
00:00:35.360 seem that way. We're talking about
00:00:36.960 reducing bandwidth by 90 plus% and speed
00:00:40.079 improvements by 20 to 30%. Which sounds
00:00:43.040 crazy just going from SSE to websocket,
00:00:45.520 right? Well, yeah. If you don't know how
00:00:48.239 AI models work, believe it or not, this
00:00:50.719 video is going to be a deep dive both
00:00:52.399 into networking technology and into the
00:00:55.120 weird characteristics of how AI models
00:00:57.440 and agents in particular actually do the
00:00:59.920 work they do. If you don't yet
00:01:01.600 understand the relationship between the
00:01:03.039 thing you're running in your terminal or
00:01:04.400 your editor and the actual servers that
00:01:06.400 are generating the outputs that go
00:01:08.000 there, this is going to be a very useful
00:01:09.600 video. And if you do already understand
00:01:11.439 that, this will probably be even more
00:01:12.960 useful and will be a good resource for
00:01:14.320 you to send to your co-workers who
00:01:15.600 almost certainly don't get any of this.
00:01:17.360 I am regularly surprised by just how
00:01:19.360 many developers don't understand the way
00:01:21.680 context works and how requests work back
00:01:23.920 and forth and any of that at all. And I
00:01:25.840 hope this will help you guys understand
00:01:27.280 it a lot better. But there's one other
00:01:28.960 thing I need you all to understand
00:01:30.080 first. How great today's sponsor is. How
00:01:32.159 many times have you had a change you
00:01:33.360 were ready to ship, but you were blocked
00:01:34.720 waiting for CI to build just for it to
00:01:36.720 finish and give you a random error you
00:01:38.159 had to go fix and then you're waiting
00:01:39.439 again for sometimes hours just to get
00:01:41.759 small changes out. That's because the CI
00:01:43.920 tools we're using suck. Unless you're
00:01:45.920 using today's sponsor, Blacksmith, they
00:01:47.680 make your CI comically faster. Even for
00:01:50.320 my simple TypeScript code bases, build
00:01:52.079 times are often cut in half from over 3
00:01:54.079 minutes to under a minute for so many
00:01:56.320 projects I work on. And if you happen to
00:01:58.159 be using Docker, the 40 times faster
00:02:00.640 Docker builds are a godsend. All of your
00:02:03.040 Docker layers are cached on NVMe drives
00:02:05.040 on the same computer. So you end up with
00:02:07.119 way faster caching, downloading, and the
00:02:09.679 whole build process with Docker. They
00:02:11.360 have really good examples on the site,
00:02:12.640 including the Android GitHub actions,
00:02:14.080 which used to take 9 minutes to run on
00:02:15.920 official GitHub CI, but with a oneline
00:02:18.160 change, it's now 3 minutes, and it costs
00:02:20.560 a fourth as much. We've been using them
00:02:22.480 for all of our CI at ping, and the team
00:02:24.480 is hooked. Whenever a new repo appears
00:02:26.160 and we start adding CI, we realize we
00:02:28.160 forgot to add Blacksmith. We make the
00:02:29.840 one line of code change and suddenly our
00:02:31.440 builds take under a minute again. It's
00:02:32.959 great. And you'll see here, even for
00:02:34.480 some of our bigger projects, our builds
00:02:36.239 take consistently 50ish seconds. I would
00:02:39.200 go in depth on how good the
00:02:40.239 observability is, but I'll just sit here
00:02:41.920 and talk for hours. It's so much better
00:02:44.080 than anything GitHub has published in
00:02:45.920 decades. It's insane. If you actually
00:02:48.000 want to be able to debug your actions,
00:02:49.680 you need to move to Blacksmith. If you
00:02:51.120 want to pay half as much for your
00:02:52.319 actions, you should use Blacksmith. If
00:02:53.840 you want your CI to be way faster, you
00:02:55.360 should use Blacksmith. If you want your
00:02:56.879 Docker bills to be way faster, you
00:02:58.239 should use Blacksmith. Generally
00:02:59.599 speaking, you should probably check them
00:03:00.800 out at soy.link/blacksmith.
00:03:03.280 So, why am I so excited about this
00:03:05.280 websocket move? The first thing I want
00:03:07.200 to make clear is that we're not yet
00:03:08.400 using this for T3 chat. And honestly,
00:03:10.000 for the T3 chat use case, it doesn't
00:03:11.920 make a ton of sense. But there are other
00:03:14.640 places where this makes a absurd amount
00:03:17.120 of sense. First, I want to make sure we
00:03:18.959 understand how context management and
00:03:21.519 requests work with an AI model. So, we
00:03:23.920 have the system prompt up top here. We
00:03:25.920 then have the user message. Let's do a
00:03:29.519 traditional one like, I want to improve
00:03:31.680 the SEO of my application. Let's write a
00:03:34.400 thorough plan on how we're going to make
00:03:36.080 this app have better SEO. Classic
00:03:38.799 standard user message. And we'll have, I
00:03:41.920 don't know, red be the response from the
00:03:45.040 agent. And the agent says, &quot;Okay, first
00:03:48.400 let's build a better understanding
00:03:52.720 of this codebase.&quot; So user makes a
00:03:55.120 prompt. Agent decides it needs to go
00:03:57.680 understand the codebase better. So what
00:03:59.439 does it do? It calls tools. I don't know
00:04:03.200 uh lsa or something where it's going to
00:04:05.920 list all of the stuff in a given
00:04:08.080 directory. What does that tool call?
00:04:10.239 Then it says interesting. The important
00:04:13.680 code is probably in source slash. So,
00:04:17.358 let's scan that next. You get the idea.
00:04:21.680 And after a bunch of those happen, we
00:04:24.000 end up with the final answer. Your SEO
00:04:26.400 has been improved. I edited D. You get
00:04:30.000 the idea. We've all built with agents.
00:04:31.360 We've all seen this. This is your
00:04:33.680 context in that moment. And obviously,
00:04:36.400 as things go, the context builds. You
00:04:38.639 have the system prompt at the top, then
00:04:40.160 the user message, then an agent message.
00:04:42.400 The agent message ends with a tool call.
00:04:44.479 The tool call fires, gets results, the
00:04:46.639 agent then responds, makes the next set
00:04:49.280 of tool calls, and then eventually you
00:04:51.040 get the actual result. You get the idea.
00:04:53.840 So, what happens when you want to do a
00:04:56.800 follow-up message? Like, I don't know,
00:04:59.440 let's say at the end here, you say that
00:05:01.840 looks good, but I want to change the
00:05:04.639 description or something like that. What
00:05:06.960 request gets sent to the OpenAI API when
00:05:11.120 you send this follow-up message? What is
00:05:14.000 contained within that? You've been
00:05:16.720 around for a bit, you know. And if we
00:05:18.479 look at our chat, you see it. All of it.
00:05:21.280 Everything. Everything. Yep, you guys
00:05:23.360 got it. Follow-up request isn't just you
00:05:26.000 sending your new message. It's you
00:05:28.320 sending the whole thing. But that's not
00:05:31.199 even where the pain point is. If that
00:05:33.280 was all, it wouldn't be too big a deal.
00:05:35.360 Sending all of this text once per user
00:05:37.680 message would be fine. What if I told
00:05:39.680 you it was sent significantly more than
00:05:41.600 that, though? What if I told you it got
00:05:44.160 sent here when you sent the user
00:05:46.000 message, here when the first tool call
00:05:48.160 completed, here when the second tool
00:05:50.000 call completed, and then again when you
00:05:51.759 sent the new prompt. Every single tool
00:05:54.160 call sends the whole history back. And
00:05:56.639 it makes sense if you think about it
00:05:58.800 because here we have the agent call.
00:06:01.919 Okay, first let's build a better
00:06:03.199 understanding of the codebase. and it
00:06:04.960 outputs a tool call. The agent is just
00:06:07.520 autocomplete. It's just generating
00:06:09.440 words. It doesn't know what's happening.
00:06:11.440 It doesn't know what system it is. It
00:06:12.880 doesn't know how long it will take.
00:06:14.319 People seem to think that when the tool
00:06:16.240 call happens, a pause occurs, but that
00:06:18.960 is not what's happening. A tool call
00:06:21.199 means the generation finished. The AI
00:06:23.759 isn't running when the tool call
00:06:25.440 happens. The AI is paused effectively.
00:06:28.000 It's dead. It's killed. It's off. And
00:06:30.319 when the tool call is done, everything
00:06:32.880 from here up gets sent back to the AI,
00:06:36.479 sent back over the API call to whatever
00:06:38.639 service is running it. And now it has a
00:06:40.479 new history that it is using to continue
00:06:42.720 generation from. Somebody asked, is this
00:06:44.560 to verify the tool call worked? No, it's
00:06:46.160 to have the information from the tool
00:06:47.680 call. The tool call was list stuff. It
00:06:50.080 was an ls call. So now there's an
00:06:52.080 output, but it can't do anything until
00:06:54.560 it has that output. So it just stops. So
00:06:57.120 every time a tool call happens, the
00:06:58.720 model needs to know what the tool call
00:07:00.240 result is. So once the tool call is
00:07:02.400 done, it then sends everything back
00:07:04.080 because it needs everything. It's almost
00:07:05.919 like a Groundhog Day type thing where
00:07:07.599 every time the model wakes up, there's
00:07:09.199 nothing left in its brain. It's
00:07:10.319 stateless. It has nothing going on. So
00:07:12.400 all of the state has to be shipped back
00:07:14.400 every single time. So if you like watch
00:07:16.880 your network traffic when you're doing a
00:07:19.120 long agent run, every one of these tool
00:07:21.440 calls is sending the whole history back
00:07:24.000 up to the anthropic API to the OpenAI
00:07:26.319 API to whatever else. And then whatever
00:07:28.560 GPU is available takes in all of that
00:07:31.120 context and then loads it for the next
00:07:33.919 response. And I already know what you
00:07:36.000 guys are thinking. Well, isn't that what
00:07:37.840 caching's for? The cache is just to
00:07:39.919 reduce the compute the model has to do,
00:07:41.599 which makes it way cheaper and also
00:07:43.680 faster to get the first token, but it's
00:07:45.759 not changing how much data you send
00:07:47.680 because the key for that cache is a hash
00:07:50.319 of your history. So if this part's all
00:07:52.880 been cached, you're not telling the AI,
00:07:56.000 hey, go pull from that cache and then
00:07:58.000 add this to it. You're passing the whole
00:08:00.560 history and then it hashes it and says,
00:08:02.720 oh we have the history up to this
00:08:04.800 point cached. The new part is here. So,
00:08:07.919 we're going to load the state of things
00:08:09.919 up until that new thing. Then, we're
00:08:12.160 going to add the new thing. Then, we're
00:08:13.440 going to start responding. That's what
00:08:14.960 the cache does. The cache does not
00:08:17.039 change how much data you send. The cache
00:08:19.360 just changes how long it takes to
00:08:21.280 process the data. It's effectively a
00:08:23.280 hash for that. So, the cache is just a
00:08:25.759 compute reduction method. Is not
00:08:27.280 changing what traffic has to be sent at
00:08:28.800 all. Want to make sure that's very, very
00:08:30.639 clear. Someone asked if this is what
00:08:32.799 compaction is. And no, compaction is
00:08:34.799 very different. Compaction couldn't be
00:08:36.399 more different. Compaction is taking the
00:08:38.640 entire history and summarizing it so you
00:08:41.360 don't need all of the tokens anymore.
00:08:43.200 But compaction breaks the cache. It does
00:08:45.519 not help the cache. Compaction is
00:08:47.440 choosing to forgo the cache because you
00:08:49.279 want to have a shorter history. Anyways,
00:08:51.600 hopefully you can somewhat see the
00:08:53.519 problem. Now, the problem isn't that
00:08:55.519 it's too hard for the model to get this
00:08:57.680 data. It's very simply that sending all
00:09:00.880 of this up every single time a tool call
00:09:03.839 happens. A growing amount of data every
00:09:06.560 single time just kind of sucks. It's
00:09:09.279 really inefficient. It's really
00:09:10.959 expensive. It uses a shitload of
00:09:13.120 bandwidth. It means running a ton of
00:09:14.480 agents in parallel is more expensive
00:09:16.000 than it needs to be. It means OpenAI's
00:09:18.080 APIs need to be able to handle these
00:09:20.240 giant text payloads and the routing and
00:09:22.720 all of those things. Wouldn't it be much
00:09:24.880 easier if instead of sending all of
00:09:27.120 this, it only had to send the new thing,
00:09:30.160 the tool call. Wouldn't that be cool?
00:09:32.399 But that makes things more complex
00:09:34.640 because effectively OpenAI's
00:09:36.080 infrastructure doesn't really allow
00:09:38.080 that. So OpenAI has a bunch of GPUs, but
00:09:40.480 you're not accessing the GPUs. You're
00:09:42.320 not sending this request straight to a
00:09:44.720 GPU, obviously, because then we have to
00:09:46.640 format everything entirely differently.
00:09:47.839 It make no sense. And also that GPU
00:09:49.839 might not have your specific cache on
00:09:51.600 it. So effectively all of these requests
00:09:54.080 have to go through an orchestration
00:09:55.760 layer, an API if you will. So in front
00:09:58.240 of OpenAI's GPUs, they have this API.
00:10:01.200 And of course, they don't have one GPU,
00:10:02.959 they have thousands of them, tens of
00:10:04.880 thousands of them across lots of
00:10:06.320 different places. So your request goes
00:10:08.240 to the API. Once your request goes to
00:10:10.320 the API, it checks things. It checks
00:10:12.240 like, do we have a cached value for
00:10:14.480 where we were before here? Do we have a
00:10:16.560 GPU open that we can route this to? Does
00:10:18.800 this user have permission to do this
00:10:20.640 thing? And then it routes whatever it
00:10:22.800 concludes here over to a GPU. And if
00:10:25.440 that goes well, it responds with those
00:10:27.360 tokens. And those tokens then get sent
00:10:29.680 back to you, which ends up being the
00:10:31.839 agent response. But also remember the
00:10:34.399 API is not as simple as it may seem
00:10:36.160 here. This is also lots of separate API
00:10:39.440 boxes that all look for random available
00:10:42.959 GPUs and route to those. So your first
00:10:45.040 request might go to the first API box.
00:10:47.279 Your second request might go to the
00:10:48.640 fourth API box and then this one might
00:10:50.959 go to a different GPU entirely. You see
00:10:54.079 the problem here? Even if the servers
00:10:57.360 were to maintain this state, making sure
00:10:59.680 the right one has the right context is
00:11:01.920 obnoxious. And I'm pretty sure the
00:11:03.839 caching layer is a whole separate third
00:11:06.000 layer thing that has to manage all of
00:11:07.760 this. So when you send the request, it
00:11:09.279 goes to a server that OpenAI runs that
00:11:11.680 is a relatively dumb server that they
00:11:13.200 have tons of that has to go check which
00:11:16.000 cache might have the cached value for
00:11:18.880 your tokens and then load that state
00:11:21.200 into a GPU, load your prompt into the
00:11:23.839 GPU, generate tokens, and then respond.
00:11:27.040 And get this, it has to do all of that
00:11:29.519 for generations that are sometimes only
00:11:31.200 10 tokens. Since this API layer doesn't
00:11:34.640 have your history, doesn't know what you
00:11:36.640 sent before because every additional
00:11:38.480 request gets routed somewhere else, you
00:11:40.640 have to resend the whole thing because
00:11:41.839 otherwise it loses it. They could try to
00:11:43.680 like attach an ID the whole time and
00:11:46.399 keep everything cached always, but they
00:11:47.920 would never know how long they have to
00:11:49.120 keep it around. The latency problems
00:11:51.120 there of making that globally scaled
00:11:52.880 across it, like not viable, not viable
00:11:55.440 at all. The architecture for that would
00:11:57.600 have to be an external data store that
00:11:59.680 all the APIs connect to that every
00:12:02.399 single request has to be cached in. Not
00:12:05.279 viable. Not viable at all. Not for that
00:12:07.040 amount of data. And not for things where
00:12:09.040 half the time it doesn't even matter.
00:12:10.560 And remember, sometimes these responses
00:12:12.720 are going to be really short like maybe
00:12:14.880 it's wrong directory trying.
00:12:18.800 This is a real thing. The agent might
00:12:20.160 respond. The agent might ingest a
00:12:22.959 100,000 tokens that you sent over API.
00:12:25.680 It might literally be sent two megabytes
00:12:28.079 of text and then respond with eight
00:12:31.760 words. That's a real thing that happens
00:12:34.480 to us every single day. You are
00:12:36.560 processing so much data for just a few
00:12:39.279 tokens to be generated. That's so
00:12:41.040 wasteful. And there's basically nothing
00:12:42.720 you can do at this layer that keeps it
00:12:45.760 scaled as well as it is that also
00:12:47.760 persists the state externally. So it
00:12:50.320 doesn't matter which you hit throughout.
00:12:52.720 There is a solution though. What if you
00:12:55.440 could guarantee that every single
00:12:57.920 request hits the same box? What if you
00:13:00.480 could be sure that tool call 2 goes the
00:13:03.519 same box that tool call one did? What if
00:13:05.680 you could be sure that all of these
00:13:07.680 follow-ups, all of these things
00:13:09.040 happening during this session are
00:13:10.639 hitting the same box? Then you wouldn't
00:13:12.560 have to load all of this from storage.
00:13:14.320 You wouldn't have to check what's cached
00:13:15.760 you already know. You wouldn't even have
00:13:17.279 to send up all of the context. You could
00:13:19.360 just send the new message or just the
00:13:21.040 new tool call. You wouldn't have to send
00:13:22.720 much of anything. But it would have to
00:13:24.800 be stateful. The way that OpenAI's API
00:13:27.440 worked previously was effectively
00:13:29.600 stateless. These boxes didn't know
00:13:31.839 anything. They had to go look up data.
00:13:34.320 They had to go check the database to see
00:13:36.320 if you had a cache entity or not. They
00:13:37.920 had to go check the O server to make
00:13:39.440 sure you were allowed to do the thing.
00:13:40.959 And every single tool call had to
00:13:42.720 doublech checkck every single thing.
00:13:45.200 Every request had to check everything.
00:13:47.519 But if the first request had everything
00:13:49.120 covered, does the second request really
00:13:50.639 need all of that? Like what if it just
00:13:52.399 knew after the first request who you
00:13:54.320 were, what you were doing, what cache
00:13:55.760 you could hit. This is why we're talking
00:13:57.600 about websockets today. The value of
00:13:59.680 websockets here isn't that it's a better
00:14:01.680 protocol. It's not that it is magically
00:14:03.760 faster cuz websockets are this gift from
00:14:05.680 the heavens. It is very simple.
00:14:07.680 Websockets can guarantee that you're
00:14:09.360 hitting the same box. Since you're
00:14:10.959 maintaining the connection to that API
00:14:13.120 server through the whole generation, you
00:14:15.279 don't have to worry about it losing its
00:14:16.720 state. You don't have to worry about
00:14:18.240 rechecking off. You don't have to worry
00:14:20.240 about knowing what's cached or not. The
00:14:22.320 websocket is less a protocol here, more
00:14:25.680 a guarantee. The guarantee is that as
00:14:28.399 this generation is running, you can keep
00:14:30.560 hitting the same server box that is now
00:14:32.399 capable of keeping track of what you've
00:14:34.240 done so far. You don't have to recheck
00:14:36.079 your O. You don't have to resend the
00:14:38.000 whole context. You just send what's new
00:14:40.320 and then it can immediately connect to a
00:14:42.160 GPU, feed everything over there, and
00:14:43.760 start responding. This reduces how much
00:14:45.760 data you send. This reduces how much
00:14:47.519 data has to be processed. This reduces
00:14:49.360 how many checks have to be done, how
00:14:50.720 much cache has to be touched, how many
00:14:52.399 things have to occur from when the tool
00:14:54.639 call completes on your machine to when
00:14:56.880 the API is aware of what's going on.
00:14:59.680 That is awesome. And it is insane it
00:15:01.680 took this long for something like this
00:15:02.959 to happen. And I am very, very, very
00:15:04.880 thankful OpenAI are the ones to do it
00:15:06.720 because OpenAI's implementation for
00:15:08.560 these things tends to become the
00:15:10.000 standard. Not only are OpenAI standards
00:15:12.399 cloned by pretty much everyone, they
00:15:14.639 actually fully open sourced it. Open
00:15:17.040 responses is a standard that is inspired
00:15:19.040 by the OpenAI responses API. Binds a
00:15:21.279 shared request response model, streaming
00:15:22.720 semantics, and tool invocation pattern.
00:15:24.399 So clients and providers can exchange
00:15:26.079 structured inputs and outputs in a
00:15:27.760 consistent shape. You can use this shape
00:15:30.160 for calling the APIs for everything from
00:15:32.480 anthropic to Gemini. And now it's an
00:15:34.639 open standard. And sadly, no, the
00:15:37.279 standard does not have the websocket
00:15:39.600 piece implemented yet. I would expect
00:15:42.720 this to be implemented very, very soon.
00:15:46.240 Yeah, I'm very thankful who is involved
00:15:48.399 with all of this. This being implemented
00:15:50.480 the way it was, the place it was,
00:15:52.079 massively increases the chance that this
00:15:53.920 does not just become standard number 72
00:15:56.880 and instead becomes a new default that
00:15:59.040 these agentic tools can use. And to be
00:16:01.199 very very clear, just to be
00:16:02.560 extraordinarily clear, the benefit here
00:16:04.720 is not huge in like the chat app use
00:16:08.079 case because persisting that connection
00:16:10.880 is not worth it if you're not following
00:16:12.959 up immediately. So the second user
00:16:15.120 message here is probably not going to go
00:16:17.120 over the same websocket and it's
00:16:18.399 probably not going to hit the same API
00:16:19.519 box. And that's fine. Reloading all of
00:16:21.759 that context once per user message is
00:16:24.800 totally reasonable. Doing it once per
00:16:26.800 tool call where sometimes one user
00:16:28.880 message could spawn hundreds of tool
00:16:30.880 calls that is much less okay. And if
00:16:33.199 you're curious about the results, this
00:16:34.639 is what OpenAI had to say. And as
00:16:36.000 somebody who was lucky enough to test
00:16:37.120 this early, I can confirm it might even
00:16:38.800 be crazier than what they're sharing
00:16:40.079 here. Websockets keep a persistent
00:16:41.680 connection to the responses API,
00:16:43.199 allowing you to send only new inputs
00:16:44.959 instead of sending round trips for the
00:16:47.440 entire context on every turn. By
00:16:49.519 maintaining inmemory state across
00:16:50.800 interactions, it avoids repeated work
00:16:52.320 and speeds up agentic runs with 20 plus
00:16:54.320 tool calls by 20 to 40%. And you can see
00:16:57.680 what this looks like in their little
00:16:58.880 demo video.
00:17:08.400 Do you understand how massive this is?
00:17:15.599 There are so many things that matter in
00:17:17.520 this space that aren't just how quick
00:17:19.439 can the model generate tokens. When we
00:17:21.760 say we're early to an extent, this is
00:17:23.679 the stuff we're talking about. On one
00:17:25.199 hand, this is so cool. Like, yeah, we
00:17:27.599 now have a way to keep things in state
00:17:29.600 and massively improve the performance.
00:17:31.600 On the other hand, how crazy early are
00:17:34.400 we that we were just okay with sending
00:17:36.799 the whole context every single time this
00:17:39.280 type of thing was generated because we
00:17:40.960 just stapled on tool calling to existing
00:17:43.360 APIs and standards. Like there's so much
00:17:45.679 opportunity here. I hope this gets you
00:17:47.679 excited to build. There's so much space
00:17:50.320 to make useful improvements across the
00:17:52.799 entire stack. So many things we just do
00:17:55.280 a certain way because that's how it
00:17:56.799 started. So many tools we still rely on
00:17:58.799 because they're just what we're used to.
00:18:00.640 Everything in our stack from how we
00:18:02.320 network to how we send requests to how
00:18:04.320 we store our code in Git is all up for
00:18:06.880 change right now. This is really fun. To
00:18:10.080 those thinking that deep tech planning
00:18:12.320 and thought and process are dead as a
00:18:14.640 result of AI, you're wrong. It matters
00:18:16.799 more than ever. We get to rethink
00:18:18.640 everything from first principles right
00:18:20.320 now. And it's fun. It's genuinely fun. I
00:18:22.880 was so hyped when OpenAI told me about
00:18:24.480 the changes they were making here. I was
00:18:25.919 really lucky to get to play with it a
00:18:27.120 bit early. And I'm so thankful that I
00:18:29.520 get to share this all with you guys now
00:18:30.960 because as nerdy as this is and as much
00:18:32.720 as I expect this video to bomb, I really
00:18:35.280 hope it does well because this stuff is
00:18:36.640 so much cooler than just whatever the
00:18:38.080 new model is. I am genuinely more
00:18:39.679 excited about this than Codex Spark than
00:18:41.919 about Sonnet 46 than about basically any
00:18:44.080 of the openweight models that have come
00:18:45.360 out recently. I'm personally way more
00:18:46.880 hyped about the small API change. Yeah.
00:18:49.919 Thank you all for nerding out with me. I
00:18:51.360 hope you enjoy this as much as I do.
00:18:52.640 Until next time, peace nerds.
