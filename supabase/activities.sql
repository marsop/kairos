create table if not exists public.activities (
    id uuid not null,
    user_id uuid not null references auth.users(id) on delete cascade,
    name text not null,
    display_order integer not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    primary key (user_id, id)
);

create index if not exists activities_user_order_idx
    on public.activities (user_id, display_order);

create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

drop trigger if exists activities_set_updated_at on public.activities;
create trigger activities_set_updated_at
before update on public.activities
for each row execute function public.set_updated_at();

alter table public.activities enable row level security;

drop policy if exists "Users can read own activities" on public.activities;
create policy "Users can read own activities"
on public.activities
for select
using (auth.uid() = user_id);

drop policy if exists "Users can insert own activities" on public.activities;
create policy "Users can insert own activities"
on public.activities
for insert
with check (auth.uid() = user_id);

drop policy if exists "Users can update own activities" on public.activities;
create policy "Users can update own activities"
on public.activities
for update
using (auth.uid() = user_id)
with check (auth.uid() = user_id);

drop policy if exists "Users can delete own activities" on public.activities;
create policy "Users can delete own activities"
on public.activities
for delete
using (auth.uid() = user_id);
