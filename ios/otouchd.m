// otouchd — raw touch stream over USB (jailbroken iOS, root daemon)
// Listens on :5050. On Windows, forward with iproxy:  iproxy 5050 :5050
// Also: otouchd --debug   → prints raw fields to stdout so you can calibrate.
//
// Build: clang otouchd.m -framework Foundation -framework IOKit -o otouchd
// Install: cp otouchd /var/jb/usr/local/bin/ && chmod +x ... (path varies by jailbreak)
// Run:     otouchd &        (or a launchd plist, see README)

#import <Foundation/Foundation.h>
#import <IOKit/IOKitLib.h>
#include <arpa/inet.h>
#include <unistd.h>
#include <errno.h>

// ---- private touch API declarations (headers come from iOSOpenDev / dump) ----
typedef struct __IOHIDEvent* IOHIDEventRef;
typedef struct __IOHIDEventSystemClient* IOHIDEventSystemClientRef;
typedef struct __IOHIDService* IOHIDServiceRef;
extern IOHIDEventSystemClientRef IOHIDEventSystemClientCreateSimpleClient(CFAllocatorRef);
extern void IOHIDEventSystemClientRegisterEventCallback(IOHIDEventSystemClientRef,
     void (*)(void*, void*, IOHIDServiceRef, IOHIDEventRef), void*, void*);
extern void IOHIDEventSystemClientScheduleWithRunLoop(IOHIDEventSystemClientRef, CFRunLoopRef, CFStringRef);
extern void IOHIDEventSystemClientSetMatching(IOHIDEventSystemClientRef, CFDictionaryRef);
extern CFArrayRef IOHIDEventGetChildren(IOHIDEventRef);
extern int IOHIDEventGetType(IOHIDEventRef);
extern double IOHIDEventGetFloatValue(IOHIDEventRef, int /*IOHIDEventFieldTouch...*/);

enum { kIOHIDEventTypeTouch = 3, kIOHIDEventTypeCollection = 11 };

// --- IOHIDEventFieldTouch* constants (adjust to your device's IOHIDEvent.h) ---
#ifndef kIOHIDEventFieldTouchAbsoluteX
#define kIOHIDEventFieldTouchAbsoluteX (1 << 12 | 4)
#endif
#ifndef kIOHIDEventFieldTouchAbsoluteY
#define kIOHIDEventFieldTouchAbsoluteY (1 << 12 | 8)
#endif
#ifndef kIOHIDEventFieldTouchPressure
#define kIOHIDEventFieldTouchPressure (1 << 12 | 3)
#endif

static int g_sock = -1;
static BOOL g_debug = NO;
static double g_last_x, g_last_y;

static void send_line(const char* s, int n) {
    if (g_sock < 0) return;
    if (send(g_sock, s, n, MSG_NOSIGNAL) < 0) { close(g_sock); g_sock = -1; }
}

static void touch_cb(void* target, void* refcon, IOHIDServiceRef svc, IOHIDEventRef event) {
    if (IOHIDEventGetType(event) != kIOHIDEventTypeCollection) return;
    CFArrayRef kids = IOHIDEventGetChildren(event);
    BOOL any = NO;
    if (kids) {
        for (CFIndex i = 0; i < CFArrayGetCount(kids); i++) {
            IOHIDEventRef t = (IOHIDEventRef)CFArrayGetValueAtIndex(kids, i);
            if (IOHIDEventGetType(t) != kIOHIDEventTypeTouch) continue;
            double x = IOHIDEventGetFloatValue(t, kIOHIDEventFieldTouchAbsoluteX);
            double y = IOHIDEventGetFloatValue(t, kIOHIDEventFieldTouchAbsoluteY);
            double p = IOHIDEventGetFloatValue(t, kIOHIDEventFieldTouchPressure);
            if (g_debug && i == 0)
                printf("TOUCH raw: x=%f y=%f p=%f\n", x, y, p), fflush(stdout);
            any = YES;
            g_last_x = x; g_last_y = y;
            char line[96];
            int n = snprintf(line, sizeof line, "D %.2f %.2f %.3f\n", x, y, p);
            send_line(line, n);
        }
    }
    if (!any) {
        send_line("U\n", 3); // collection arrived with no live touches = UP
        if (g_debug) printf("UP\n"), fflush(stdout);
    }
}

// accept callback on the listen socket via CFFileDescriptor
static void accept_cb(CFFileDescriptorRef fdref, CFOptionFlags flags, void* info) {
    int s = CFFileDescriptorGetNativeDescriptor(fdref);
    int c = accept(s, NULL, NULL);
    if (c < 0) return;
    if (g_sock >= 0) close(g_sock);
    g_sock = c; signal(SIGPIPE, SIG_IGN);
    send_line("W 393 852\n", 12); // logical point-size of YOUR screen; PC uses it
    CFFileDescriptorEnableCallBacks(fdref, kCFFileDescriptorReadCallBack);
}

int main(int argc, char** argv) {
    if (argc > 1 && strcmp(argv[1], "--debug") == 0) g_debug = YES;

    @autoreleasepool {
        IOHIDEventSystemClientRef c = IOHIDEventSystemClientCreateSimpleClient(kCFAllocatorDefault);
        NSMutableDictionary *m = [NSMutableDictionary new];
        m[@(0x0B /* kIOHIDEventSystemClientPrimaryUsagePage */)] = @(13 /* kHIDPage_Digitizer */);
        m[@(0x0F /* kIOHIDEventSystemClientPrimaryUsage */)] = @(0);
        IOHIDEventSystemClientSetMatching(c, (__bridge CFDictionaryRef)m);
        IOHIDEventSystemClientRegisterEventCallback(c, touch_cb, NULL, NULL);
        IOHIDEventSystemClientScheduleWithRunLoop(c, CFRunLoopGetCurrent(), (__bridge CFStringRef)@"Default");

        int s = socket(AF_INET, SOCK_STREAM, 0);
        struct sockaddr_in sa = { .sin_family = AF_INET, .sin_addr.s_addr = INADDR_ANY, .sin_port = htons(5050) };
        bind(s, (struct sockaddr*)&sa, sizeof sa);
        listen(s, 4);

        CFFileDescriptorRef fdref = CFFileDescriptorCreate(kCFAllocatorDefault, s, false, accept_cb, NULL);
        CFFileDescriptorEnableCallBacks(fdref, kCFFileDescriptorReadCallBack);
        CFRunLoopSourceRef src = CFFileDescriptorCreateRunLoopSource(kCFAllocatorDefault, fdref, 0);
        CFRunLoopAddSource(CFRunLoopGetCurrent(), src, kCFRunLoopDefaultMode);

        printf("otouchd: listening :5050 %s\n", g_debug ? "(debug)" : "");
        CFRunLoopRun();
    }
    return 0;
}
