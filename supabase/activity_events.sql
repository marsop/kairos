create table if not exists public.activity_events (
    id uuid not null,
    user_id uuid not null references auth.users(id) on delete cascade,
    activity_id uuid null,
    start_time timestamptz not null,
    end_time timestamptz null,
    activity_name text not null,
    activity_emoji text not null default '',
    activity_color text not null default '#10B981',
    comment text not null default '',
    metadata text not null default '',
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    primary key (user_id, id)
);

alter table public.activity_events
    add column if not exists activity_emoji text not null default '';

alter table public.activity_events
    add column if not exists activity_color text not null default '#10B981';

alter table public.activity_events
    add column if not exists comment text not null default '';

alter table public.activity_events
    add column if not exists metadata text not null default '';

create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

drop trigger if exists activity_events_set_updated_at on public.activity_events;
create trigger activity_events_set_updated_at
before update on public.activity_events
for each row execute function public.set_updated_at();

alter table public.activity_events enable row level security;

drop policy if exists "Users can read own activity events" on public.activity_events;
create policy "Users can read own activity events"
on public.activity_events
for select
using (auth.uid() = user_id);

drop policy if exists "Users can insert own activity events" on public.activity_events;
create policy "Users can insert own activity events"
on public.activity_events
for insert
with check (auth.uid() = user_id);

drop policy if exists "Users can update own activity events" on public.activity_events;
create policy "Users can update own activity events"
on public.activity_events
for update
using (auth.uid() = user_id)
with check (auth.uid() = user_id);

drop policy if exists "Users can delete own activity events" on public.activity_events;
create policy "Users can delete own activity events"
on public.activity_events
for delete
using (auth.uid() = user_id);

do $$
begin
    if not exists (
        select 1
        from pg_publication_tables
        where pubname = 'supabase_realtime'
          and schemaname = 'public'
          and tablename = 'activity_events'
    ) then
        execute 'alter publication supabase_realtime add table public.activity_events';
    end if;
end;
$$;
