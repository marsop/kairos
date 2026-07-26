create table if not exists public.budget_settings (
    user_id uuid primary key references auth.users(id) on delete cascade,
    minimum_enabled boolean not null default false,
    threshold integer not null default 95 check (threshold >= 75 and threshold <= 99),
    color_minimum_not_reached text not null default '#0000ff',
    color_minimum_reached_max_not_reached text not null default '#00ff00',
    color_between_threshold_max text not null default '#ffff00',
    color_over_max text not null default '#ff0000',
    budget_type integer not null default 0,
    notifications_enabled boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

drop trigger if exists budget_settings_set_updated_at on public.budget_settings;
create trigger budget_settings_set_updated_at
before update on public.budget_settings
for each row execute function public.set_updated_at();

alter table public.budget_settings enable row level security;

drop policy if exists "Users can read own budget settings" on public.budget_settings;
create policy "Users can read own budget settings"
on public.budget_settings
for select
using (auth.uid() = user_id);

drop policy if exists "Users can insert own budget settings" on public.budget_settings;
create policy "Users can insert own budget settings"
on public.budget_settings
for insert
with check (auth.uid() = user_id);

drop policy if exists "Users can update own budget settings" on public.budget_settings;
create policy "Users can update own budget settings"
on public.budget_settings
for update
using (auth.uid() = user_id)
with check (auth.uid() = user_id);

drop policy if exists "Users can delete own budget settings" on public.budget_settings;
create policy "Users can delete own budget settings"
on public.budget_settings
for delete
using (auth.uid() = user_id);

do $$
begin
    if not exists (
        select 1
        from pg_publication_tables
        where pubname = 'supabase_realtime'
          and schemaname = 'public'
          and tablename = 'budget_settings'
    ) then
        execute 'alter publication supabase_realtime add table public.budget_settings';
    end if;
end;
$$;
