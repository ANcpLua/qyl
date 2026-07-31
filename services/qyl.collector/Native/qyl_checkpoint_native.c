#if defined(__APPLE__)
#define QYL_O_CREAT 0x0200
#define QYL_O_EXCL 0x0800
typedef unsigned short qyl_mode_t;
#elif defined(__linux__)
#define QYL_O_CREAT 0x0040
#define QYL_O_EXCL 0x0080
typedef unsigned int qyl_mode_t;
#else
#error "qyl checkpoint native creation supports only Linux and macOS"
#endif

extern int openat(int directory, const char *path, int flags, ...);

__attribute__((visibility("default")))
int qyl_openat_create(
    int directory,
    const char *path,
    int flags,
    unsigned int mode)
{
    return openat(
        directory,
        path,
        flags | QYL_O_CREAT | QYL_O_EXCL,
        (qyl_mode_t)mode);
}
