/** Lightweight shell shown while a route chunk loads — keeps sidebar visible context. */
const RouteLoadingSkeleton = () => (
  <div className="flex h-screen mesh-gradient-bg overflow-hidden md:gap-3 animate-pulse">
    <div className="hidden md:block w-[240px] shrink-0 m-3 rounded-2xl bg-white/30" />
    <main className="flex-1 m-3 rounded-2xl bg-white/40 p-6 space-y-4">
      <div className="h-8 w-48 rounded-lg bg-white/50" />
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="h-24 rounded-xl bg-white/50" />
        <div className="h-24 rounded-xl bg-white/50" />
        <div className="h-24 rounded-xl bg-white/50" />
      </div>
      <div className="h-64 rounded-xl bg-white/50" />
    </main>
  </div>
);

export default RouteLoadingSkeleton;
