create extension if not exists pgcrypto;

create table if not exists public.user_activity_groups (
    user_id uuid not null references auth.users(id) on delete cascade,
    group_id uuid not null default gen_random_uuid(),
    group_order integer not null check (group_order >= 0),
    name text not null default '',
    color text not null default '#10B981',
    icon text not null default '🗂️',
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    primary key (user_id, group_id)
);

create unique index if not exists user_activity_groups_user_order_uidx
    on public.user_activity_groups (user_id, group_order);

create index if not exists user_activity_groups_user_id_idx
    on public.user_activity_groups (user_id);

create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

drop trigger if exists user_activity_groups_set_updated_at on public.user_activity_groups;
create trigger user_activity_groups_set_updated_at
before update on public.user_activity_groups
for each row execute function public.set_updated_at();

alter table public.user_activity_groups enable row level security;

drop policy if exists "Users can read own activity groups" on public.user_activity_groups;
create policy "Users can read own activity groups"
on public.user_activity_groups
for select
using (auth.uid() = user_id);

drop policy if exists "Users can insert own activity groups" on public.user_activity_groups;
create policy "Users can insert own activity groups"
on public.user_activity_groups
for insert
with check (auth.uid() = user_id);

drop policy if exists "Users can update own activity groups" on public.user_activity_groups;
create policy "Users can update own activity groups"
on public.user_activity_groups
for update
using (auth.uid() = user_id)
with check (auth.uid() = user_id);

drop policy if exists "Users can delete own activity groups" on public.user_activity_groups;
create policy "Users can delete own activity groups"
on public.user_activity_groups
for delete
using (auth.uid() = user_id);

do $$
begin
    if not exists (
        select 1
        from pg_publication_tables
        where pubname = 'supabase_realtime'
          and schemaname = 'public'
          and tablename = 'user_activity_groups'
    ) then
        execute 'alter publication supabase_realtime add table public.user_activity_groups';
    end if;
end;
$$;
