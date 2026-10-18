# Reference model of the cost formulas (one channel, k bits, FixedWindowFirst)
K=8
def afw_fits(prev, x, w, k=K):
    W=1<<w; s=prev-(W>>1)
    s=max(0,min(s,(1<<k)-W))
    return s<=x<s+W
def fixed_len(xs,p,w,maxL):
    mn=mx=xs[p]; l=0
    while l<maxL and p+l<len(xs):
        v=xs[p+l]; mn2=min(mn,v); mx2=max(mx,v)
        if mx2-mn2>(1<<w)-1: break
        mn,mx=mn2,mx2; l+=1
    return l
def afw_len(xs,p,w,maxL):
    l=1
    while l<maxL and p+l<len(xs) and afw_fits(xs[p+l-1],xs[p+l],w): l+=1
    return l
def methods(xs,p,maxL,k=K):
    # (name, length, cost(l))
    ms=[]
    for w in range(k):
        L=fixed_len(xs,p,w,maxL); ms.append(('F%d'%w,L,lambda l,w=w:k+l*w))
    ms.append(('U',min(maxL,len(xs)-p),lambda l:l*k))
    for w in range(1,k):
        L=afw_len(xs,p,w,maxL); ms.append(('A%d'%w,L,lambda l,w=w:k+(l-1)*w))
    return ms
def bits(xs,p,l,H,maxL):
    best=None
    for n,L,c in methods(xs,p,maxL):
        if L>=l and (best is None or c(l)<best[0]): best=(c(l),n)
    return H+best[0],best[1]
def dp(xs,b,d=4):
    H=b+d; maxL=1<<b; N=len(xs); best=[0]*(N+1); arg=[0]*N
    for p in range(N-1,-1,-1):
        bb=None
        for l in range(1,min(maxL,N-p)+1):
            t=bits(xs,p,l,H,maxL)[0]+best[p+l]
            if bb is None or t<bb: bb=t; arg[p]=l
        best[p]=bb
    chain=[];p=0
    while p<N: l=arg[p]; chain.append((p,l)+bits(xs,p,l,H,maxL)); p+=l
    return best,chain
def greedy(xs,b,d=4):
    # Sequential search: at each position the block (method) with the best efficiency, block length = method length
    H=b+d; maxL=1<<b; N=len(xs); p=0; chain=[]
    while p<N:
        best=None
        for n,L,c in methods(xs,p,maxL):
            e=1-(H+c(L))/(L*K)
            if best is None or e>best[0]: best=(e,n,L,H+c(L))
        chain.append((p,best[2],best[3],best[1])); p+=best[2]
    return chain
